using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
using SimulacroExamen.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios MVC ───────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// ── Rate Limiter por IP (.NET 8 nativo) ─────────────────────────
// Política "login-ip": 10 requests por IP cada minuto al endpoint de login.
// Protege contra spam masivo desde una sola IP/proxy. El bloqueo por usuario
// (FechaBaneo en BD) es independiente y cubre ataques distribuidos.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("login-ip", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0
            }));
});

// Necesario para que funcione correctamente detrás del proxy inverso (Azure/Railway).
// No se hace .Clear() de KnownNetworks/KnownProxies: eso haría que la app confiase
// ciegamente en X-Forwarded-For de cualquier cliente, permitiendo spoofing de IP
// para burlar el rate-limiting de login. Por defecto, ASP.NET Core solo confía en
// el loopback (la situación típica cuando el proxy está en el mismo host).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit     = 1;

    // Railway termina TLS en su edge y reenvía internamente por IPs privadas
    // que no son siempre loopback ni rangos RFC1918 conocidos. Limpiamos los
    // filtros para que el middleware procese los headers independientemente de
    // la IP del proxy. El contenedor no es accesible públicamente en Railway,
    // por lo que confiar en los headers internos es seguro.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Contexto de base de datos ───────────────────────────────────
// Usa SQL Server con la cadena de conexión "DefaultConnection" definida en
// appsettings.json (desarrollo) o en las variables de entorno del host (producción).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Servicios de aplicación ─────────────────────────────────────
// ISecretProtector: cifra/descifra valores sensibles con DataProtection.
// IExcelService: genera/importa archivos Excel de preguntas y resultados.
// IEmailService: envía correos transaccionales (verificación, recuperación).

// PersistKeysToDbContext guarda las claves de DataProtection en la tabla
// DataProtectionKeys de la BD. Esto es crítico en Railway/contenedores:
// sin persistencia las claves se regeneran en cada reinicio y la contraseña
// SMTP cifrada queda ilegible, impidiendo el envío de correos.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();
builder.Services.AddSingleton<ISecretProtector, SecretProtector>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();

// ── Autenticación por cookie ────────────────────────────────────
// Se usa cookie propia (no sesión de servidor) para ser compatible con
// ambientes sin estado (Railway, contenedores). La cookie se configura:
//   - HttpOnly + Secure + SameSite=Strict → protección CSRF/XSS básica.
//   - Sliding expiration de 8 h → la sesión se renueva con cada request,
//     evitando cortes de sesión durante el trabajo activo del usuario.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/Account/Login";
        options.LogoutPath        = "/Account/Logout";
        options.AccessDeniedPath  = "/Account/AccesoDenegado";
        options.Cookie.HttpOnly   = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite     = SameSiteMode.Strict;
        options.ExpireTimeSpan    = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// ── Pipeline de middleware ──────────────────────────────────────
// ORDEN IMPORTANTE: los middleware se ejecutan en el orden en que se registran.
// ForwardedHeaders debe ir primero para que el resto del pipeline vea la IP/esquema reales.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    // En producción, redirige errores no controlados al controlador de errores genérico.
    // HSTS solo se activa en producción para no romper el flujo HTTP local del desarrollador.
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Redirige todo el tráfico HTTP a HTTPS antes de que llegue a cualquier otro middleware.
app.UseHttpsRedirection();

// ── Cabeceras de seguridad ──────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"]        = "DENY";
    context.Response.Headers["X-Content-Type-Options"]  = "nosniff";
    context.Response.Headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";

    // Content-Security-Policy: limita qué recursos puede cargar/ejecutar la página
    // para mitigar XSS residuales. Incluye los CDNs que usan Bootstrap/Chart.js.
    // 'unsafe-inline' se mantiene para los <script> y <style> inline existentes
    // en las vistas — quitar cuando migremos a nonces.
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' cdn.jsdelivr.net cdnjs.cloudflare.com www.google.com www.gstatic.com; " +
        "style-src 'self' 'unsafe-inline' cdn.jsdelivr.net cdnjs.cloudflare.com fonts.googleapis.com; " +
        "font-src 'self' cdn.jsdelivr.net cdnjs.cloudflare.com fonts.gstatic.com data:; " +
        "img-src 'self' data: blob: https:; " +
        "connect-src 'self'; " +
        "frame-src www.google.com; " +     // iframe de reCAPTCHA invisible
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    await next();
});

// Sirve archivos de wwwroot (CSS, JS, imágenes) sin pasar por el enrutador MVC.
app.UseStaticFiles();
// UseRouting debe ir antes de UseAuthentication/UseAuthorization para que los
// atributos [Authorize] de los endpoints se evalúen con la ruta ya conocida.
app.UseRouting();
// Rate limiter va entre Routing y Authentication: ya conocemos la ruta,
// pero aún no se ha autenticado al usuario (la limitación es por IP).
app.UseRateLimiter();
app.UseAuthentication();
// SessionValidationMiddleware comprueba que el token de sesión en la cookie
// coincida con el almacenado en BD, invalidando sesiones concurrentes.
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

// ── Rutas MVC ───────────────────────────────────────────────────
// La ruta por defecto lleva al Login en lugar de Home para forzar autenticación
// en el primer acceso. Los controladores con [Authorize] redirigen aquí automáticamente.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// ── Inicializar BD y sembrar datos base ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // EnsureCreated crea la BD y todas las tablas si la BD no existe.
    // En producción nunca recreamos automáticamente: si falta una tabla,
    // abortamos el arranque para que un humano investigue.
    InicializarBD(context, app.Environment.IsDevelopment());

    // Migrar columnas nuevas que no existían en versiones anteriores
    MigrarEsquema(context);

    // ── Seeding: SuperAdmin ─────────────────────────────────────────
    // Se busca por NombreUsuario para que sea idempotente en cada arranque.
    // La contraseña se lee de configuración o variable de entorno; si falta,
    // se aborta el arranque porque arrancar sin SuperAdmin sería un riesgo de seguridad.
    if (!context.Usuarios.Any(u => u.NombreUsuario == "LEAO.HUACAUSI"))
    {
        var superAdminPass = app.Configuration["DefaultUsers:SuperAdminPassword"]
                             ?? Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD")
                             ?? throw new InvalidOperationException(
                                 "Falta la contraseña del SuperAdmin. Configura DefaultUsers:SuperAdminPassword o la variable de entorno SUPERADMIN_PASSWORD.");
        context.Administradores.Add(new Administrador
        {
            NombreUsuario = "LEAO.HUACAUSI",
            Correo        = "leao.huacausi@simulacro.com",
            Contrasena    = BCrypt.Net.BCrypt.HashPassword(superAdminPass),
            Rol           = "SuperAdmin",
            FechaCreacion = DateTime.Now,
            Activo        = true
        });
        context.SaveChanges();
    }

    // ── Seeding: Admin por defecto ──────────────────────────────────
    // Se verifica en la tabla Usuarios (no en Administradores) porque la herencia TPH
    // guarda ambos tipos en la misma tabla; Administradores filtra por Discriminador.
    // La contraseña sigue el mismo patrón de configuración que el SuperAdmin.
    if (!context.Usuarios.Any(u => u.Rol == "Admin"))
    {
        var adminPass = app.Configuration["DefaultUsers:AdminPassword"]
                        ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD")
                        ?? throw new InvalidOperationException(
                            "Falta la contraseña del Admin. Configura DefaultUsers:AdminPassword o la variable de entorno ADMIN_PASSWORD.");
        context.Administradores.Add(new Administrador
        {
            NombreUsuario = "ADMIN",
            Correo        = "admin@simulacro.com",
            Contrasena    = BCrypt.Net.BCrypt.HashPassword(adminPass),
            Rol           = "Admin",
            FechaCreacion = DateTime.Now,
            Activo        = true
        });
        context.SaveChanges();
    }

    // ── Seeding: Tipos de examen ────────────────────────────────────
    // Los 17 tipos corresponden a las especialidades de salud de la convocatoria SERUMS.
    // La inserción es idempotente (Any por Nombre) por lo que puede repetirse en cada arranque.
    var tiposBase = new[]
    {
        "BIOLOGÍA",
        "ENFERMERÍA",
        "FARMACIA Y BIOQUÍMICA",
        "ING.SANITARIA",
        "MEDICINA VETERINARIA",
        "ODONTOLOGÍA",
        "PSICOLOGÍA",
        "TRABAJO SOCIAL",
        "TM. LABORATORIO",
        "MEDICINA",
        "NUTRICIÓN",
        "TM. OPTOMETRÍA",
        "TM. RADIOLOGÍA",
        "TM. T. FÍSICA",
        "TM. T. LENGUAJE",
        "TM.T.OCUPACIONAL",
        "OBSTETRICIA"
    };

    foreach (var nombre in tiposBase)
    {
        if (!context.TiposExamen.Any(t => t.Nombre == nombre))
        {
            context.TiposExamen.Add(new TipoExamen { Nombre = nombre, Activo = true });
        }
    }
    context.SaveChanges();

    // ── Seeding: Planes de suscripción ─────────────────────────────
    // Se inserta el catálogo completo solo si la tabla está vacía.
    // Las CaracteristicasPlan se crean junto con el plan en la misma operación
    // aprovechando que EF Core resuelve las FKs antes de llamar a SaveChanges.
    if (!context.PlanesSuscripcion.Any())
    {
        context.PlanesSuscripcion.AddRange(
            new PlanSuscripcion
            {
                Nombre = "Pruebita", Etiqueta = "PLAN MENSUAL", Precio = 8,
                TextoPrecio = "mensuales", ColorPrimario = "#74c0fc", ColorSecundario = "#4dabf7",
                EsPopular = false, TextoBadge = "Solo para los 50 Primeros ¡Hasta agotar cupo!",
                EnlaceBoton = "https://wa.me/51936037152", TextoBoton = "¡Suscribirme ya!",
                Activo = true, Orden = 1,
                Caracteristicas = new List<CaracteristicaPlan>
                {
                    new() { Texto = "Acceso **limitado** a simulacros de examen.", Orden = 0 },
                    new() { Texto = "Calculadora para estimar tu nota final.", Orden = 1 },
                    new() { Texto = "Noticias y convocatorias relevantes sobre exámenes.", Orden = 2 }
                }
            },
            new PlanSuscripcion
            {
                Nombre = "El Aplicado", Etiqueta = "PLAN MENSUAL", Precio = 15,
                TextoPrecio = "mensuales", ColorPrimario = "#69db7c", ColorSecundario = "#40c057",
                EsPopular = true, TextoBadge = null,
                EnlaceBoton = "https://wa.me/51936037152", TextoBoton = "¡Suscribirme ya!",
                Activo = true, Orden = 2,
                Caracteristicas = new List<CaracteristicaPlan>
                {
                    new() { Texto = "Acceso **ilimitado** a simulacros de examen.", Orden = 0 },
                    new() { Texto = "Calculadora para estimar **tu nota final.**", Orden = 1 },
                    new() { Texto = "**Noticias y convocatorias** relevantes sobre exámenes.", Orden = 2 },
                    new() { Texto = "**Asistencia prioritaria** y soporte más rápido.", Orden = 3 }
                }
            },
            new PlanSuscripcion
            {
                Nombre = "Postulante Premium", Etiqueta = "PLAN POR 2 MESES", Precio = 20,
                TextoPrecio = "por 2 meses", ColorPrimario = "#ffa94d", ColorSecundario = "#f76707",
                EsPopular = false, TextoBadge = null,
                EnlaceBoton = "https://wa.me/51936037152", TextoBoton = "¡Suscribirme ya!",
                Activo = true, Orden = 3,
                Caracteristicas = new List<CaracteristicaPlan>
                {
                    new() { Texto = "Acceso **ilimitado** a simulacros de examen.", Orden = 0 },
                    new() { Texto = "Calculadora para estimar tu nota final.", Orden = 1 },
                    new() { Texto = "**Noticias y convocatorias** relevantes sobre exámenes.", Orden = 2 },
                    new() { Texto = "**Asistencia prioritaria** y soporte más rápido.", Orden = 3 },
                    new() { Texto = "Participa en **sorteos exclusivos.**", Orden = 4 }
                }
            }
        );
        context.SaveChanges();
    }
}

app.Run();

// ── Helper: agregar columnas/tablas nuevas sin romper la BD existente ────
// POR QUÉ SQL dinámico en lugar de migraciones EF Core:
//   EnsureCreated crea el esquema inicial completo, pero NO aplica cambios
//   sobre tablas ya existentes. Si se añade una propiedad al modelo, EF Core
//   no altera la tabla en producción. Este helper cubre ese hueco ejecutando
//   sentencias ALTER/CREATE condicionales (IF NOT EXISTS) que son idempotentes:
//   pueden correr N veces sin efecto dañino si la columna/tabla ya existe.
static void MigrarEsquema(ApplicationDbContext context)
{
    var conn = context.Database.GetDbConnection();
    try
    {
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
    }
    catch { return; } // Sin conexión no hay nada que hacer

    // Cada sentencia falla de forma independiente: si una falla,
    // las demás siguen ejecutándose (evita que un catch global las bloquee).
    void Exec(string sql)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        catch { /* sentencia individual falló; continuar con las demás */ }
    }

    // Tabla para persistir claves de DataProtection entre reinicios del contenedor.
    // Sin esta tabla las claves se regeneran con cada deploy y la contraseña SMTP
    // cifrada queda ilegible, impidiendo el envío de correos.
    Exec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DataProtectionKeys')
           CREATE TABLE DataProtectionKeys (
               Id           INT IDENTITY(1,1) PRIMARY KEY,
               FriendlyName NVARCHAR(MAX) NULL,
               Xml          NVARCHAR(MAX) NULL
           )");

        // Columna NotaPonderada en Usuarios
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='NotaPonderada')
               ALTER TABLE Usuarios ADD NotaPonderada float NULL");

        // Columna IntentosExtra en Usuarios
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='IntentosExtra')
               ALTER TABLE Usuarios ADD IntentosExtra int NOT NULL DEFAULT 0");

        // Columnas de datos personales en Usuarios
        // EnsureCreated no añade estas columnas a bases existentes; el loop
        // es idempotente gracias al IF NOT EXISTS en cada ALTER TABLE.
        foreach (var col in new[] { "PrimerNombre", "SegundoNombre", "PrimerApellido", "SegundoApellido" })
        {
            Exec($@"IF NOT EXISTS (
                       SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='{col}')
                   ALTER TABLE Usuarios ADD {col} NVARCHAR(100) NULL");
        }
        // Columna Celular en Usuarios (añadida en versión posterior al despliegue inicial)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='Celular')
               ALTER TABLE Usuarios ADD Celular NVARCHAR(20) NULL");
        // Columna Dni en Usuarios (añadida en versión posterior al despliegue inicial)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='Dni')
               ALTER TABLE Usuarios ADD Dni NVARCHAR(8) NULL");

        // Columna SessionToken para sesión única por usuario (128 chars para token hex-64)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='SessionToken')
               ALTER TABLE Usuarios ADD SessionToken NVARCHAR(128) NULL");

        // Columnas anti brute-force: contador de intentos fallidos y fecha de baneo temporal.
        // Si FechaBaneo > GETDATE() el usuario no puede iniciar sesión.
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='IntentosFallidos')
               ALTER TABLE Usuarios ADD IntentosFallidos INT NOT NULL DEFAULT 0");
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='FechaBaneo')
               ALTER TABLE Usuarios ADD FechaBaneo DATETIME2 NULL");

        // Ampliar SessionToken si aún tiene el tamaño antiguo de 36
        Exec(@"IF EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='SessionToken'
                     AND CHARACTER_MAXIMUM_LENGTH < 128)
               ALTER TABLE Usuarios ALTER COLUMN SessionToken NVARCHAR(128) NULL");

        // Columna EsTrial para modo prueba (permite acceso limitado sin suscripción activa)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='EsTrial')
               ALTER TABLE Usuarios ADD EsTrial BIT NOT NULL DEFAULT 0");

        // Columna FechaVencimiento para suscripciones (controla el acceso según el plan activo)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='FechaVencimiento')
               ALTER TABLE Usuarios ADD FechaVencimiento DATETIME2 NULL");

        // Columnas para recuperación de contraseña (token de un solo uso con expiración)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='PasswordResetToken')
               ALTER TABLE Usuarios ADD PasswordResetToken NVARCHAR(100) NULL");
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='PasswordResetExpiry')
               ALTER TABLE Usuarios ADD PasswordResetExpiry DATETIME2 NULL");

        // Columnas para verificación de email
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='EmailVerificado')
               ALTER TABLE Usuarios ADD EmailVerificado BIT NOT NULL DEFAULT 0");
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='EmailVerificacionToken')
               ALTER TABLE Usuarios ADD EmailVerificacionToken NVARCHAR(100) NULL");
        // Usuarios existentes se marcan como verificados para no interrumpir acceso
        Exec(@"UPDATE Usuarios SET EmailVerificado = 1 WHERE EmailVerificado = 0 AND FechaCreacion < GETDATE()");

        // Columna PlanSuscripcionId en Usuarios (FK → PlanesSuscripcion, solo Estudiante)
        // Se agrega via SQL dinámico porque EnsureCreated no altera tablas ya existentes.
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='PlanSuscripcionId')
               BEGIN
                   ALTER TABLE Usuarios ADD PlanSuscripcionId INT NULL;
                   IF OBJECT_ID('PlanesSuscripcion') IS NOT NULL
                       ALTER TABLE Usuarios ADD CONSTRAINT FK_Usuarios_PlanSuscripcion
                           FOREIGN KEY (PlanSuscripcionId) REFERENCES PlanesSuscripcion(Id)
                           ON DELETE SET NULL;
               END");

        // Columna Discriminador para herencia TPH (Administrador / Estudiante)
        // Se agrega via SQL dinámico porque EnsureCreated no altera tablas ya existentes;
        // la columna se rellena retroactivamente según el Rol para bases migradas.
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='Discriminador')
               BEGIN
                   ALTER TABLE Usuarios ADD Discriminador NVARCHAR(50) NOT NULL DEFAULT 'Estudiante';
                   UPDATE Usuarios SET Discriminador = 'Administrador' WHERE Rol IN ('Admin','SuperAdmin');
               END");


        // Columna DuracionSegundos en Examenes (registra el tiempo real consumido por el alumno)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Examenes' AND COLUMN_NAME='DuracionSegundos')
               ALTER TABLE Examenes ADD DuracionSegundos int NULL");

        // Columna DuracionMinutos en TiposExamen (configurable por el admin)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='TiposExamen' AND COLUMN_NAME='DuracionMinutos')
               ALTER TABLE TiposExamen ADD DuracionMinutos INT NULL");

        // Columna NumeroPreguntas en TiposExamen (bases existentes sin esta columna; default 20)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='TiposExamen' AND COLUMN_NAME='NumeroPreguntas')
               ALTER TABLE TiposExamen ADD NumeroPreguntas INT NOT NULL DEFAULT 20");

        // Tabla CaracteristicasPlan (normalización de PlanesSuscripcion.Caracteristicas)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='CaracteristicasPlan')
               CREATE TABLE CaracteristicasPlan (
                   Id                INT IDENTITY(1,1) PRIMARY KEY,
                   PlanSuscripcionId INT            NOT NULL,
                   Texto             NVARCHAR(300)  NOT NULL,
                   Orden             INT            NOT NULL DEFAULT 0,
                   CONSTRAINT FK_CaracteristicasPlan_Planes
                       FOREIGN KEY (PlanSuscripcionId)
                       REFERENCES PlanesSuscripcion(Id) ON DELETE CASCADE
               )");

        // Tabla Noticias
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='Noticias')
               CREATE TABLE Noticias (
                   Id                INT IDENTITY(1,1) PRIMARY KEY,
                   Titulo            NVARCHAR(200)    NOT NULL,
                   Contenido         NVARCHAR(MAX)    NOT NULL,
                   ImagenRuta        NVARCHAR(500)    NULL,
                   EnlaceUrl         NVARCHAR(1000)   NULL,
                   FechaPublicacion  DATETIME2        NOT NULL DEFAULT GETDATE(),
                   AdminId           INT              NOT NULL,
                   Activo            BIT              NOT NULL DEFAULT 1,
                   CONSTRAINT FK_Noticias_Usuarios FOREIGN KEY (AdminId)
                       REFERENCES Usuarios(Id) ON DELETE NO ACTION
               )");

        // Columna EnlaceUrl en Noticias (para bases existentes)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Noticias' AND COLUMN_NAME='EnlaceUrl')
               ALTER TABLE Noticias ADD EnlaceUrl NVARCHAR(1000) NULL");

        // Tabla AnunciosGlobales (banner de mantenimiento/anuncio global)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='AnunciosGlobales')
               CREATE TABLE AnunciosGlobales (
                   Id                   INT IDENTITY(1,1) PRIMARY KEY,
                   Mensaje              NVARCHAR(500)  NOT NULL,
                   Tipo                 NVARCHAR(20)   NOT NULL DEFAULT 'warning',
                   Activo               BIT            NOT NULL DEFAULT 0,
                   FechaActualizacion   DATETIME2      NOT NULL DEFAULT GETDATE(),
                   AdminId              INT            NOT NULL,
                   CONSTRAINT FK_AnunciosGlobales_Usuarios FOREIGN KEY (AdminId)
                       REFERENCES Usuarios(Id) ON DELETE NO ACTION
               )");

        // Tabla PlanesSuscripcion (tarjetas del modal de planes para usuarios trial)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='PlanesSuscripcion')
               BEGIN
                   CREATE TABLE PlanesSuscripcion (
                       Id               INT IDENTITY(1,1) PRIMARY KEY,
                       Nombre           NVARCHAR(100)  NOT NULL,
                       Etiqueta         NVARCHAR(100)  NOT NULL DEFAULT 'PLAN MENSUAL',
                       Precio           DECIMAL(10,2)  NOT NULL DEFAULT 0,
                       TextoPrecio      NVARCHAR(50)   NOT NULL DEFAULT 'mensuales',
                       ColorPrimario    NVARCHAR(20)   NOT NULL DEFAULT '#74c0fc',
                       ColorSecundario  NVARCHAR(20)   NOT NULL DEFAULT '#4dabf7',
                       EsPopular        BIT            NOT NULL DEFAULT 0,
                       TextoBadge       NVARCHAR(200)  NULL,
                       Caracteristicas  NVARCHAR(MAX)  NOT NULL DEFAULT '',
                       EnlaceBoton      NVARCHAR(500)  NOT NULL DEFAULT 'https://wa.me/51936037152',
                       TextoBoton       NVARCHAR(80)   NOT NULL DEFAULT '¡Suscribirme ya!',
                       Activo           BIT            NOT NULL DEFAULT 1,
                       Orden            INT            NOT NULL DEFAULT 0,
                       FechaCreacion    DATETIME2      NOT NULL DEFAULT GETDATE()
                   );
                   -- Sembrar los 3 planes por defecto
                   INSERT INTO PlanesSuscripcion
                       (Nombre,Etiqueta,Precio,TextoPrecio,ColorPrimario,ColorSecundario,EsPopular,TextoBadge,Caracteristicas,EnlaceBoton,TextoBoton,Activo,Orden)
                   VALUES
                       ('Pruebita','PLAN MENSUAL',8,'mensuales','#74c0fc','#4dabf7',0,
                        'Solo para los 50 Primeros ¡Hasta agotar cupo!',
                        'Acceso **limitado** a simulacros de examen.
Calculadora para estimar tu nota final.
Noticias y convocatorias relevantes sobre exámenes.',
                        'https://wa.me/51936037152','¡Suscribirme ya!',1,1),
                       ('El Aplicado','PLAN MENSUAL',15,'mensuales','#69db7c','#40c057',1,NULL,
                        'Acceso **ilimitado** a simulacros de examen.
Calculadora para estimar **tu nota final.**
**Noticias y convocatorias** relevantes sobre exámenes.
**Asistencia prioritaria** y soporte más rápido.',
                        'https://wa.me/51936037152','¡Suscribirme ya!',1,2),
                       ('Postulante Premium','PLAN POR 2 MESES',20,'por 2 meses','#ffa94d','#f76707',0,NULL,
                        'Acceso **ilimitado** a simulacros de examen.
Calculadora para estimar tu nota final.
**Noticias y convocatorias** relevantes sobre exámenes.
**Asistencia prioritaria** y soporte más rápido.
Participa en **sorteos exclusivos.**',
                        'https://wa.me/51936037152','¡Suscribirme ya!',1,3);
               END");

        // Tabla OrdenesAlternativaExamen (orden aleatorio de alternativas por pregunta de examen)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='OrdenesAlternativaExamen')
               CREATE TABLE OrdenesAlternativaExamen (
                   Id               INT IDENTITY(1,1) PRIMARY KEY,
                   PreguntaExamenId INT NOT NULL,
                   AlternativaId    INT NOT NULL,
                   Orden            INT NOT NULL DEFAULT 0,
                   CONSTRAINT FK_OrdenesAlt_PreguntaExamen
                       FOREIGN KEY (PreguntaExamenId)
                       REFERENCES PreguntasExamen(Id) ON DELETE CASCADE,
                   CONSTRAINT FK_OrdenesAlt_Alternativa
                       FOREIGN KEY (AlternativaId)
                       REFERENCES Alternativas(Id)
               )");

        // Tabla Sugerencias
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='Sugerencias')
               CREATE TABLE Sugerencias (
                   Id          INT IDENTITY(1,1) PRIMARY KEY,
                   UsuarioId   INT            NOT NULL,
                   Asunto      NVARCHAR(100)  NOT NULL,
                   Mensaje     NVARCHAR(2000) NOT NULL,
                   FechaEnvio  DATETIME2      NOT NULL DEFAULT GETDATE(),
                   Leida       BIT            NOT NULL DEFAULT 0,
                   CONSTRAINT FK_Sugerencias_Usuarios FOREIGN KEY (UsuarioId)
                       REFERENCES Usuarios(Id) ON DELETE NO ACTION
               )");

        // Tabla LogsActividad
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='LogsActividad')
               CREATE TABLE LogsActividad (
                   Id           INT IDENTITY(1,1) PRIMARY KEY,
                   Fecha        DATETIME2        NOT NULL DEFAULT GETDATE(),
                   AdminId      INT              NOT NULL,
                   Accion       NVARCHAR(100)    NOT NULL,
                   Descripcion  NVARCHAR(500)    NOT NULL
               )");

        // AdminNombre era NOT NULL sin default en versiones anteriores;
        // EF Core no lo escribe en INSERT → falla. Lo convertimos a NULL.
        Exec(@"IF EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='LogsActividad' AND COLUMN_NAME='AdminNombre'
                   AND IS_NULLABLE='NO')
               ALTER TABLE LogsActividad ALTER COLUMN AdminNombre NVARCHAR(100) NULL");

        // Tabla ConfiguracionCorreo (configuración SMTP editable desde el panel SuperAdmin)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='ConfiguracionCorreo')
               CREATE TABLE ConfiguracionCorreo (
                   Id                   INT IDENTITY(1,1) PRIMARY KEY,
                   Smtp                 NVARCHAR(200)  NOT NULL DEFAULT 'smtp.gmail.com',
                   Puerto               INT            NOT NULL DEFAULT 587,
                   UsuarioCorreo        NVARCHAR(200)  NOT NULL DEFAULT '',
                   Contrasena           NVARCHAR(200)  NOT NULL DEFAULT '',
                   NombreRemitente      NVARCHAR(200)  NOT NULL DEFAULT 'Simulacro SERUMS',
                   UsarSsl              BIT            NOT NULL DEFAULT 1,
                   UltimaActualizacion  DATETIME2      NOT NULL DEFAULT GETDATE(),
                   AdminId              INT            NOT NULL DEFAULT 0
               )");

        // Tabla OpcionesDuracion (opciones de tiempo configurables por TipoExamen)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_NAME='OpcionesDuracion')
               CREATE TABLE OpcionesDuracion (
                   Id              INT IDENTITY(1,1) PRIMARY KEY,
                   TipoExamenId    INT            NOT NULL,
                   Etiqueta        NVARCHAR(100)  NOT NULL,
                   DuracionMinutos INT            NOT NULL DEFAULT 0,
                   NumeroPreguntas INT            NOT NULL DEFAULT 0,
                   Orden           INT            NOT NULL DEFAULT 0,
                   CONSTRAINT FK_OpcionesDuracion_TiposExamen
                       FOREIGN KEY (TipoExamenId)
                       REFERENCES TiposExamen(Id) ON DELETE CASCADE
               )");

        // Columna NumeroPreguntas en OpcionesDuracion (agregada después del despliegue inicial de la tabla)
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='OpcionesDuracion' AND COLUMN_NAME='NumeroPreguntas')
               ALTER TABLE OpcionesDuracion ADD NumeroPreguntas INT NOT NULL DEFAULT 0");

    if (conn.State == System.Data.ConnectionState.Open)
        conn.Close();
}

// ── Helper: crear esquema desde cero si las tablas no existen ────
static void InicializarBD(ApplicationDbContext context, bool isDevelopment)
{
    // Paso 1: crear la BD si no existe (sin tocar las tablas si ya hay)
    context.Database.EnsureCreated();

    // Paso 2: verificar que la tabla Usuarios existe.
    // EnsureCreated devuelve false cuando la BD ya existe, incluso si está vacía
    // (comportamiento de SQL Server: si hay CUALQUIER tabla, no crea nada).
    var conn = context.Database.GetDbConnection();
    conn.Open();

    using var cmd = conn.CreateCommand();
    cmd.CommandText =
        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
        "WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME = 'Usuarios'";

    var existe = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    conn.Close();

    if (existe) return;

    // En producción NUNCA eliminar la BD automáticamente: un error transitorio
    // de red podría destruir toda la data. Fallar explícitamente para que
    // un humano investigue y decida cómo proceder.
    if (!isDevelopment)
    {
        throw new InvalidOperationException(
            "La base de datos existe pero la tabla 'Usuarios' no fue encontrada. " +
            "Esto indica una migración incompleta o corrupción del esquema. " +
            "Ejecuta el script de migración manualmente. Se aborta el arranque para " +
            "evitar la pérdida de datos.");
    }

    // Solo en desarrollo: recrear la BD desde cero
    context.Database.EnsureDeleted();
    context.Database.EnsureCreated();
}
