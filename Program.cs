using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/Account/Login";
        options.LogoutPath        = "/Account/Logout";
        options.AccessDeniedPath  = "/Account/AccesoDenegado";
        options.Cookie.HttpOnly   = true;
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
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
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

    // Sembrar administrador por defecto (contraseña cifrada con BCrypt)
    if (!context.Usuarios.Any(u => u.Rol == "Admin"))
    {
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
