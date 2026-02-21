using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.ViewModels;
using System.Security.Claims;

namespace SimulacroExamen.Controllers
{
    /// <summary>
    /// Controlador de autenticación. Gestiona el inicio y cierre de sesión.
    /// No requiere [Authorize] porque es accedido por usuarios no autenticados.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;

        /// <summary>Recibe el DbContext por inyección de dependencias.</summary>
        public AccountController(ApplicationDbContext db) => _db = db;

        // ── GET /Account/Login ───────────────────────────────────────
        /// <summary>
        /// Muestra el formulario de inicio de sesión.
        /// Si el usuario ya está autenticado, lo redirige directamente según su rol.
        /// </summary>
        /// <param name="returnUrl">URL a la que redirigir tras login (puede venir del middleware).</param>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Evita que un usuario ya logueado vea el login de nuevo
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRol();

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // ── POST /Account/Login ──────────────────────────────────────
        /// <summary>
        /// Procesa las credenciales enviadas por el formulario.
        /// 1. Valida el modelo (campos requeridos).
        /// 2. Busca el usuario activo en la BD.
        /// 3. Verifica la contraseña con BCrypt.
        /// 4. Crea los claims y emite la cookie de autenticación.
        /// 5. Redirige a returnUrl o al panel según el rol.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken] // Previene ataques CSRF
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // Solo usuarios activos pueden iniciar sesión
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == vm.NombreUsuario && u.Activo);

            // Mensaje genérico: no revelar si el usuario existe o no
            if (usuario == null || !BCrypt.Net.BCrypt.Verify(vm.Contrasena, usuario.Contrasena))
            {
                ModelState.AddModelError("", "Usuario o contraseña incorrectos");
                return View(vm);
            }

            // Claims: datos del usuario que viajarán en la cookie cifrada
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()), // ID para consultas en BD
                new Claim(ClaimTypes.Name,           usuario.NombreUsuario), // User.Identity.Name
                new Claim(ClaimTypes.Email,          usuario.Correo),
                new Claim(ClaimTypes.Role,           usuario.Rol),           // Habilita User.IsInRole()
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // IsPersistent=true: la cookie sobrevive al cierre del navegador (hasta ExpiresTimeSpan)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            // Url.IsLocalUrl() previene ataques de open redirect
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToRol();
        }

        // ── GET /Account/Logout ──────────────────────────────────────
        /// <summary>
        /// Cierra la sesión eliminando la cookie de autenticación
        /// y redirige al formulario de login.
        /// </summary>
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ── GET /Account/AccesoDenegado ──────────────────────────────
        /// <summary>
        /// Página mostrada cuando un usuario autenticado intenta acceder
        /// a un recurso para el que no tiene el rol requerido.
        /// Configurada en AddCookie(options.AccessDeniedPath).
        /// </summary>
        public IActionResult AccesoDenegado() => View();

        // ── Helpers privados ─────────────────────────────────────────
        /// <summary>
        /// Redirige al panel correspondiente según el rol del usuario autenticado.
        /// Admin → AdminController.Index | Usuario → ExamenController.Index
        /// </summary>
        private IActionResult RedirectToRol()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Admin");

            return RedirectToAction("Index", "Examen");
        }
    }
}
