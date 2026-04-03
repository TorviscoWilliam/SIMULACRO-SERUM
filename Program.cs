using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
using SimulacroExamen.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Necesario para que funcione correctamente detrás del proxy inverso de Azure
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<IEmailService, EmailService>();

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

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// ── Cabeceras de seguridad ──────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"]        = "DENY";
    context.Response.Headers["X-Content-Type-Options"]  = "nosniff";
    context.Response.Headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// ── Inicializar BD y sembrar datos base ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // EnsureCreated crea la BD y todas las tablas si la BD no existe.
    // Si la BD ya existe pero las tablas faltan (ej. BD creada manualmente o
    // DROP ejecutado), verificamos la tabla Usuarios y recreamos si es necesario.
    InicializarBD(context);

    // Migrar columnas nuevas que no existían en versiones anteriores
    MigrarEsquema(context);

    // Sembrar administrador principal (SuperAdmin) – solo si no existe
    if (!context.Usuarios.Any(u => u.NombreUsuario == "LEAO.HUACAUSI"))
    {
        context.Usuarios.Add(new Usuario
        {
            NombreUsuario = "LEAO.HUACAUSI",
            Correo        = "leao.huacausi@simulacro.com",
            Contrasena    = BCrypt.Net.BCrypt.HashPassword("Mender_2201"),
            Rol           = "SuperAdmin",
            FechaCreacion = DateTime.Now,
            Activo        = true
        });
        context.SaveChanges();
    }

    // Sembrar administrador por defecto (contraseña cifrada con BCrypt)
    if (!context.Usuarios.Any(u => u.Rol == "Admin"))
    {
        context.Usuarios.Add(new Usuario
        {
            NombreUsuario = "ADMIN",
            Correo        = "admin@simulacro.com",
            Contrasena    = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            Rol           = "Admin",
            FechaCreacion = DateTime.Now,
            Activo        = true
        });
        context.SaveChanges();
    }

    // Sembrar los 17 tipos de examen
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
}

app.Run();

// ── Helper: agregar columnas/tablas nuevas sin romper la BD existente ────
static void MigrarEsquema(ApplicationDbContext context)
{
    try
    {
        var conn = context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();

        void Exec(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

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
        foreach (var col in new[] { "PrimerNombre", "SegundoNombre", "PrimerApellido", "SegundoApellido" })
        {
            Exec($@"IF NOT EXISTS (
                       SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='{col}')
                   ALTER TABLE Usuarios ADD {col} NVARCHAR(100) NULL");
        }
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='Celular')
               ALTER TABLE Usuarios ADD Celular NVARCHAR(20) NULL");
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='Dni')
               ALTER TABLE Usuarios ADD Dni NVARCHAR(8) NULL");

        // Columna SessionToken para sesión única por usuario
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='SessionToken')
               ALTER TABLE Usuarios ADD SessionToken NVARCHAR(36) NULL");

        // Columna EsTrial para modo prueba
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='EsTrial')
               ALTER TABLE Usuarios ADD EsTrial BIT NOT NULL DEFAULT 0");

        // Columna FechaVencimiento para suscripciones
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='FechaVencimiento')
               ALTER TABLE Usuarios ADD FechaVencimiento DATETIME2 NULL");

        // Columnas para recuperación de contraseña
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


        // Columna DuracionSegundos en Examenes
        Exec(@"IF NOT EXISTS (
                   SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME='Examenes' AND COLUMN_NAME='DuracionSegundos')
               ALTER TABLE Examenes ADD DuracionSegundos int NULL");

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
                   AdminNombre  NVARCHAR(100)    NOT NULL,
                   Accion       NVARCHAR(100)    NOT NULL,
                   Descripcion  NVARCHAR(500)    NOT NULL
               )");

        conn.Close();
    }
    catch { /* Si falla la migración opcional, no bloquear el inicio */ }
}

// ── Helper: crear esquema desde cero si las tablas no existen ────
static void InicializarBD(ApplicationDbContext context)
{
    // Paso 1: crear la BD si no existe (sin tocar las tablas si ya hay)
    context.Database.EnsureCreated();

    // Paso 2: verificar que la tabla Usuarios existe.
    // EnsureCreated devuelve false cuando la BD ya existe, incluso si está vacía
    // (comportamiento de SQL Server: si hay CUALQUIER tabla, no crea nada).
    // Si Usuarios no existe, la BD está incompleta → eliminar y recrear.
    try
    {
        var conn = context.Database.GetDbConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
            "WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME = 'Usuarios'";

        var existe = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        conn.Close();

        if (!existe)
        {
            // BD existe pero sin las tablas esperadas → recrear desde cero
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        }
    }
    catch
    {
        // No se pudo conectar o verificar → forzar recreación
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
    }
}
