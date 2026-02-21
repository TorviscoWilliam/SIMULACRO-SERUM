using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ──────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IExcelService, ExcelService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath          = "/Account/Login";
        options.LogoutPath         = "/Account/Logout";
        options.AccessDeniedPath   = "/Account/AccesoDenegado";
        options.Cookie.HttpOnly    = true;
        options.ExpireTimeSpan     = TimeSpan.FromHours(8);
        options.SlidingExpiration  = true;
    });

// ── Build ──────────────────────────────────────────────────────
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

// ── Inicializar BD y sembrar administrador ──────────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

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
}

app.Run();
