using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ── 1. MVC: controladores y vistas Razor ───────────────────────
builder.Services.AddControllersWithViews();

// ── 2. Base de datos (EF Core + SQL Server) ─────────────────────
//    La cadena de conexión se lee de appsettings.json → "DefaultConnection"
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── 3. Servicio de Excel (importar preguntas / exportar usuarios)
//    Scoped: una instancia por petición, mismo ciclo de vida que el DbContext
builder.Services.AddScoped<IExcelService, ExcelService>();

// ── 4. Autenticación por cookie ─────────────────────────────────
//    HttpOnly: la cookie no es accesible desde JavaScript (previene XSS)
//    SlidingExpiration: renueva el vencimiento con cada petición activa
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/Account/Login";          // Sin sesión → redirige al login
        options.LogoutPath        = "/Account/Logout";         // Cierra sesión y borra la cookie
        options.AccessDeniedPath  = "/Account/AccesoDenegado"; // Rol sin permiso → acceso denegado
        options.Cookie.HttpOnly   = true;
        options.ExpireTimeSpan    = TimeSpan.FromHours(8);     // Sesión válida 8 horas
        options.SlidingExpiration = true;
    });

// ── 5. Build: construir la app ──────────────────────────────────
var app = builder.Build();

// En producción: página de error genérica y HSTS (refuerza HTTPS en el navegador)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection(); // Redirige peticiones HTTP a HTTPS automáticamente
app.UseStaticFiles();      // Sirve wwwroot (CSS, JS, imágenes) sin pasar por MVC
app.UseRouting();          // Analiza la URL y selecciona el controlador/acción

// IMPORTANTE: Authentication siempre antes de Authorization
app.UseAuthentication();   // Lee la cookie y puebla HttpContext.User con claims y roles
app.UseAuthorization();    // Evalúa [Authorize] y verifica los roles requeridos

// ── 6. Ruta MVC por defecto ─────────────────────────────────────
//    Patrón: /{controller}/{action}/{id?}
//    Sin ruta → redirige a Account/Login (página de inicio de la app)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// ── 7. Inicializar BD y sembrar el administrador ─────────────────
//    EnsureCreated() crea tablas según los modelos EF Core si no existen.
//    Equivale a ejecutar el script SQL de Scripts/DatabaseSetup.sql de forma automática.
//    Solo inserta el admin si no hay ninguno (primer arranque del sistema).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated(); // Crea la BD y todas las tablas si no existen

    if (!context.Usuarios.Any(u => u.Rol == "Admin"))
    {
        // BCrypt.HashPassword genera un hash con salt aleatorio: no reversible ni predecible
        context.Usuarios.Add(new Usuario
        {
            NombreUsuario = "admin",
            Correo        = "admin@simulacro.com",
            Contrasena    = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            Rol           = "Admin",
            FechaCreacion = DateTime.Now,
            Activo        = true
        });
        context.SaveChanges();
    }
}

app.Run();
