using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.ViewModels;
using System.Security.Claims;

namespace SimulacroExamen.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AccountController(ApplicationDbContext db) => _db = db;

        // ── GET /Account/Login ───────────────────────────────────────
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRol();

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // ── POST /Account/Login ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // Búsqueda case-insensitive: todos los nombres nuevos son uppercase
            var nombreBusqueda = vm.NombreUsuario.Trim().ToUpperInvariant();
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == nombreBusqueda && u.Activo);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(vm.Contrasena, usuario.Contrasena))
            {
                ModelState.AddModelError("", "Usuario o contraseña incorrectos");
                return View(vm);
            }

            // Generar token de sesión único → invalida cualquier sesión anterior
            var sessionToken = Guid.NewGuid().ToString();
            usuario.SessionToken = sessionToken;
            await _db.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name,           usuario.NombreUsuario),
                new Claim(ClaimTypes.Email,          usuario.Correo),
                new Claim(ClaimTypes.Role,           usuario.Rol),
                new Claim("SessionToken",            sessionToken),
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return usuario.Rol == "Admin"
                ? RedirectToAction("Index", "Admin")
                : RedirectToAction("Index", "Examen");
        }

        // ── GET /Account/Verificar?campo=usuario|correo|celular|dni&valor=... ───
        // Usado por AJAX en el formulario de registro para validación en tiempo real.
        [HttpGet]
        public async Task<IActionResult> Verificar(string campo, string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return Json(new { ocupado = false });

            bool ocupado = campo switch
            {
                "usuario" => await _db.Usuarios.AnyAsync(u => u.NombreUsuario == valor.Trim().ToUpperInvariant()),
                "correo"  => await _db.Usuarios.AnyAsync(u => u.Correo  == valor.Trim()),
                "celular" => await _db.Usuarios.AnyAsync(u => u.Celular == valor.Trim()),
                "dni"     => await _db.Usuarios.AnyAsync(u => u.Dni     == valor.Trim()),
                _         => false
            };

            return Json(new { ocupado });
        }

        // ── GET /Account/Register ────────────────────────────────────
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRol();

            return View(new RegisterViewModel());
        }

        // ── POST /Account/Register ───────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // ── Validar unicidad de correo, celular y DNI ────────────
            if (await _db.Usuarios.AnyAsync(u => u.Correo == vm.Correo.Trim()))
            {
                ModelState.AddModelError(nameof(vm.Correo), "Ese correo ya está registrado.");
                return View(vm);
            }

            if (await _db.Usuarios.AnyAsync(u => u.Celular == vm.Celular.Trim()))
            {
                ModelState.AddModelError(nameof(vm.Celular), "Ese número de celular ya está registrado.");
                return View(vm);
            }

            if (await _db.Usuarios.AnyAsync(u => u.Dni == vm.Dni.Trim()))
            {
                ModelState.AddModelError(nameof(vm.Dni), "Ese DNI ya está registrado.");
                return View(vm);
            }

            // ── Resolver username con fallback a SegundoNombre ───────
            var nombreUpper = vm.NombreUsuario.Trim().ToUpperInvariant();

            if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUpper))
            {
                // Calcular el username auto-generado original para detectar si el usuario
                // no lo editó manualmente (coincide con PrimerNombre.PrimerApellido)
                var primerN   = vm.PrimerNombre.Trim().Split(' ')[0].ToUpperInvariant();
                var primerA   = vm.PrimerApellido.Trim().Split(' ')[0].ToUpperInvariant();
                var usernameGen = $"{primerN}.{primerA}";

                if (nombreUpper == usernameGen && !string.IsNullOrWhiteSpace(vm.SegundoNombre))
                {
                    // Intentar fallback con SegundoNombre.PrimerApellido
                    var segundoN  = vm.SegundoNombre.Trim().Split(' ')[0].ToUpperInvariant();
                    var fallback  = $"{segundoN}.{primerA}";

                    if (!await _db.Usuarios.AnyAsync(u => u.NombreUsuario == fallback))
                    {
                        nombreUpper = fallback; // aplicar fallback automáticamente
                    }
                    else
                    {
                        ModelState.AddModelError(nameof(vm.NombreUsuario),
                            $"'{usernameGen}' y '{fallback}' ya están en uso. Por favor elige un nombre de usuario diferente.");
                        return View(vm);
                    }
                }
                else
                {
                    ModelState.AddModelError(nameof(vm.NombreUsuario), "Ese nombre de usuario ya está en uso.");
                    return View(vm);
                }
            }

            _db.Usuarios.Add(new Usuario
            {
                NombreUsuario   = nombreUpper,
                Correo          = vm.Correo.Trim(),
                Contrasena      = BCrypt.Net.BCrypt.HashPassword(vm.Contrasena),
                Rol             = "Usuario",
                FechaCreacion   = DateTime.Now,
                Activo          = true,
                PrimerNombre    = vm.PrimerNombre.Trim().ToUpperInvariant(),
                SegundoNombre   = string.IsNullOrWhiteSpace(vm.SegundoNombre) ? null : vm.SegundoNombre.Trim().ToUpperInvariant(),
                PrimerApellido  = vm.PrimerApellido.Trim().ToUpperInvariant(),
                SegundoApellido = string.IsNullOrWhiteSpace(vm.SegundoApellido) ? null : vm.SegundoApellido.Trim().ToUpperInvariant(),
                Celular         = vm.Celular.Trim(),
                Dni             = vm.Dni.Trim()
            });

            await _db.SaveChangesAsync();

            TempData["Exito"] = "Cuenta creada exitosamente. Inicia sesión.";
            return RedirectToAction(nameof(Login));
        }

        // ── GET /Account/Logout ──────────────────────────────────────
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ── GET /Account/AccesoDenegado ──────────────────────────────
        public IActionResult AccesoDenegado() => View();

        private IActionResult RedirectToRol()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Admin");

            return RedirectToAction("Index", "Examen");
        }
    }
}
