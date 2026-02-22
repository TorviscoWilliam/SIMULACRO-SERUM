using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

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
    context.Database.EnsureCreated();

    // Sembrar administrador por defecto
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
            context.TiposExamen.Add(new TipoExamen
            {
                Nombre = nombre,
                Activo = true
            });
        }
    }
    context.SaveChanges();
}

app.Run();
