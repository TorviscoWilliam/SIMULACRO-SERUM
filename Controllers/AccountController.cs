using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.ViewModels;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace SimulacroExamen.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;

        // ── Protección anti fuerza bruta (en memoria) ─────────────
        private static readonly ConcurrentDictionary<string, (int intentos, DateTime bloqueoHasta)> _loginIntentos = new();
        private const int MaxIntentos = 5;
        private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

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

            // ── Rate-limit por IP ──────────────────────────────────────
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (_loginIntentos.TryGetValue(ip, out var info) && info.bloqueoHasta > DateTime.UtcNow)
            {
                var restante = (int)(info.bloqueoHasta - DateTime.UtcNow).TotalMinutes + 1;
                ModelState.AddModelError("", $"Demasiados intentos fallidos. Intente de nuevo en {restante} minuto(s).");
                return View(vm);
            }

            // Búsqueda case-insensitive: todos los nombres nuevos son uppercase
            var nombreBusqueda = vm.NombreUsuario.Trim().ToUpperInvariant();
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == nombreBusqueda && u.Activo);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(vm.Contrasena, usuario.Contrasena))
            {
                // Incrementar contador de intentos fallidos
                var intentos = _loginIntentos.AddOrUpdate(ip,
                    _ => (1, DateTime.MinValue),
                    (_, prev) => (prev.intentos + 1, prev.bloqueoHasta));

                if (intentos.intentos >= MaxIntentos)
                    _loginIntentos[ip] = (intentos.intentos, DateTime.UtcNow.Add(DuracionBloqueo));

                ModelState.AddModelError("", "Usuario o contraseña incorrectos");
                return View(vm);
            }

            // Login exitoso → limpiar intentos
            _loginIntentos.TryRemove(ip, out _);

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

            return (usuario.Rol == "Admin" || usuario.Rol == "SuperAdmin")
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
        public async Task<IActionResult> Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRol();

            ViewBag.TiposExamen = await _db.TiposExamen
                .Where(t => t.Activo)
                .OrderBy(t => t.Nombre)
                .ToListAsync();

            return View(new RegisterViewModel());
        }

        // ── POST /Account/Register ───────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            // Recargar tipos para la vista en caso de error
            async Task RecargarTipos() =>
                ViewBag.TiposExamen = await _db.TiposExamen
                    .Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();

            if (!ModelState.IsValid)
            {
                await RecargarTipos();
                return View(vm);
            }

            // Validar que el tipo de examen exista y esté activo
            var tipoExamen = await _db.TiposExamen
                .FirstOrDefaultAsync(t => t.Id == vm.TipoExamenId && t.Activo);

            if (tipoExamen == null)
            {
                ModelState.AddModelError(nameof(vm.TipoExamenId), "El tipo de examen seleccionado no es válido.");
                await RecargarTipos();
                return View(vm);
            }

            // ── Validar unicidad de correo, celular y DNI ────────────
            if (await _db.Usuarios.AnyAsync(u => u.Correo == vm.Correo.Trim()))
            {
                ModelState.AddModelError(nameof(vm.Correo), "Ese correo ya está registrado.");
                await RecargarTipos(); return View(vm);
            }

            if (await _db.Usuarios.AnyAsync(u => u.Celular == vm.Celular.Trim()))
            {
                ModelState.AddModelError(nameof(vm.Celular), "Ese número de celular ya está registrado.");
                await RecargarTipos(); return View(vm);
            }

            if (await _db.Usuarios.AnyAsync(u => u.Dni == vm.Dni.Trim()))
            {
                ModelState.AddModelError(nameof(vm.Dni), "Ese DNI ya está registrado.");
                await RecargarTipos(); return View(vm);
            }

            // ── Resolver username con fallback a SegundoNombre ───────
            var nombreUpper = vm.NombreUsuario.Trim().ToUpperInvariant();

            if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUpper))
            {
                var primerN     = vm.PrimerNombre.Trim().Split(' ')[0].ToUpperInvariant();
                var primerA     = vm.PrimerApellido.Trim().Split(' ')[0].ToUpperInvariant();
                var usernameGen = $"{primerN}.{primerA}";

                if (nombreUpper == usernameGen && !string.IsNullOrWhiteSpace(vm.SegundoNombre))
                {
                    var segundoN = vm.SegundoNombre.Trim().Split(' ')[0].ToUpperInvariant();
                    var fallback = $"{segundoN}.{primerA}";

                    if (!await _db.Usuarios.AnyAsync(u => u.NombreUsuario == fallback))
                    {
                        nombreUpper = fallback;
                    }
                    else
                    {
                        ModelState.AddModelError(nameof(vm.NombreUsuario),
                            $"'{usernameGen}' y '{fallback}' ya están en uso. Por favor elige un nombre de usuario diferente.");
                        await RecargarTipos(); return View(vm);
                    }
                }
                else
                {
                    ModelState.AddModelError(nameof(vm.NombreUsuario), "Ese nombre de usuario ya está en uso.");
                    await RecargarTipos(); return View(vm);
                }
            }

            var nuevoUsuario = new Usuario
            {
                NombreUsuario   = nombreUpper,
                Correo          = vm.Correo.Trim(),
                Contrasena      = BCrypt.Net.BCrypt.HashPassword(vm.Contrasena),
                Rol             = "Usuario",
                FechaCreacion   = DateTime.Now,
                Activo          = true,
                EsTrial         = true,
                PrimerNombre    = vm.PrimerNombre.Trim().ToUpperInvariant(),
                SegundoNombre   = string.IsNullOrWhiteSpace(vm.SegundoNombre) ? null : vm.SegundoNombre.Trim().ToUpperInvariant(),
                PrimerApellido  = vm.PrimerApellido.Trim().ToUpperInvariant(),
                SegundoApellido = string.IsNullOrWhiteSpace(vm.SegundoApellido) ? null : vm.SegundoApellido.Trim().ToUpperInvariant(),
                Celular         = vm.Celular.Trim(),
                Dni             = vm.Dni.Trim()
            };

            _db.Usuarios.Add(nuevoUsuario);
            await _db.SaveChangesAsync();

            // Asignar acceso al tipo de examen seleccionado
            _db.UsuarioTiposExamen.Add(new UsuarioTipoExamen
            {
                UsuarioId       = nuevoUsuario.Id,
                TipoExamenId    = vm.TipoExamenId,
                FechaAsignacion = DateTime.Now
            });
            await _db.SaveChangesAsync();

            TempData["Exito"] = $"Cuenta creada exitosamente. Tienes 1 examen de prueba de {tipoExamen.Nombre}. Inicia sesión.";
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
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
                return RedirectToAction("Index", "Admin");

            return RedirectToAction("Index", "Examen");
        }
    }
}
