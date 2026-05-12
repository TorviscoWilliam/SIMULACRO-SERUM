using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
using SimulacroExamen.ViewModels;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;

namespace SimulacroExamen.Controllers
{
    /// <summary>
    /// Maneja todo el flujo de identidad del usuario: inicio de sesión,
    /// registro, cierre de sesión, recuperación de contraseña y verificación
    /// de correo electrónico.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext       _db;
        private readonly IEmailService              _email;
        private readonly ILogger<AccountController> _log;
        private readonly IRecaptchaService          _recaptcha;
        private readonly IConfiguration             _config;

        // Constantes del bloqueo por usuario (capa 1 de defensa: BD)
        private const int      MaxIntentosUsuario = 3;
        private static readonly TimeSpan DuracionBaneoUsuario = TimeSpan.FromMinutes(5);
        private const double   RecaptchaScoreMinimo = 0.5;

        // Genera un token seguro (256 bits de entropía) usando CSPRNG.
        // Sustituye a Guid.NewGuid() para tokens de sesión, verificación y reset.
        private static string GenerarTokenSeguro() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        /// <summary>
        /// Inyecta el contexto de base de datos, el servicio de correo,
        /// el servicio de reCAPTCHA y la configuración (para leer SiteKey).
        /// </summary>
        public AccountController(ApplicationDbContext db, IEmailService email,
                                 ILogger<AccountController> log,
                                 IRecaptchaService recaptcha,
                                 IConfiguration config)
        {
            _db        = db;
            _email     = email;
            _log       = log;
            _recaptcha = recaptcha;
            _config    = config;
        }

        // ── GET /Account/Login ───────────────────────────────────────
        /// <summary>
        /// Muestra el formulario de inicio de sesión.
        /// Redirige automáticamente al dashboard correspondiente si el usuario
        /// ya tiene una sesión activa.
        /// </summary>
        /// <param name="returnUrl">URL a la que redirigir tras el login exitoso (opcional).</param>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRol();

            ViewBag.RecaptchaSiteKey = _config["Recaptcha:SiteKey"];
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // ── POST /Account/Login ──────────────────────────────────────
        /// <summary>
        /// Procesa las credenciales del formulario. Aplica 3 capas defensivas:
        ///   1. Rate Limiter por IP (atributo EnableRateLimiting → 10 req/min/IP)
        ///   2. reCAPTCHA v3 (score mínimo 0.5, acción "login")
        ///   3. Bloqueo por usuario en BD (3 intentos → ban de 5 min vía FechaBaneo)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login-ip")]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            ViewBag.RecaptchaSiteKey = _config["Recaptcha:SiteKey"];

            if (!ModelState.IsValid)
                return View(vm);

            // ── Capa 2: reCAPTCHA v3 ───────────────────────────────────
            // Se valida ANTES de tocar la BD para evitar carga de DB con bots.
            var scoreOk = await _recaptcha.VerificarAsync(
                vm.RecaptchaToken, accion: "login", scoreMinimo: RecaptchaScoreMinimo);
            if (!scoreOk)
            {
                ModelState.AddModelError("", "Validación de seguridad fallida. Recarga la página e inténtalo de nuevo.");
                return View(vm);
            }

            // Sanitizar: solo letras, dígitos y punto (formato NOMBRE.APELLIDO).
            // Cualquier otro carácter se descarta antes de tocar la BD.
            var nombreBusqueda = System.Text.RegularExpressions.Regex
                .Replace(vm.NombreUsuario.Trim(), @"[^\w\.]", "")
                .ToUpperInvariant();
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == nombreBusqueda && u.Activo);

            // ── Capa 1: bloqueo por usuario en BD ──────────────────────
            // Si FechaBaneo > now, el usuario está temporalmente bloqueado.
            // Esto cubre ataques distribuidos donde el rate limiter por IP no aplica.
            if (usuario?.FechaBaneo != null && usuario.FechaBaneo > DateTime.Now)
            {
                var minutos = Math.Max(1, (int)Math.Ceiling((usuario.FechaBaneo.Value - DateTime.Now).TotalMinutes));
                ModelState.AddModelError("", $"Cuenta bloqueada por seguridad. Intenta de nuevo en {minutos} minuto(s).");
                return View(vm);
            }

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(vm.Contrasena, usuario.Contrasena))
            {
                // Solo incrementamos contador si el usuario existe (no revela enumeración).
                // Al llegar a 3, upserteamos FechaBaneo y reseteamos el contador a 0:
                // la fuente de verdad del bloqueo es FechaBaneo, no el contador.
                if (usuario != null)
                {
                    usuario.IntentosFallidos++;
                    if (usuario.IntentosFallidos >= MaxIntentosUsuario)
                    {
                        usuario.FechaBaneo       = DateTime.Now.Add(DuracionBaneoUsuario);
                        usuario.IntentosFallidos = 0;
                    }
                    try { await _db.SaveChangesAsync(); }
                    catch (Exception ex)
                    {
                        // Columnas ausentes o error de BD: no cortamos el flujo del login,
                        // pero logueamos el error completo para diagnóstico.
                        _log.LogError(ex,
                            "No se pudo persistir IntentosFallidos/FechaBaneo para usuario {Id}. " +
                            "Causa probable: columnas no migradas. Inner: {Inner}",
                            usuario.Id, ex.InnerException?.Message ?? ex.Message);
                        _db.Entry(usuario).State = EntityState.Unchanged;
                    }
                }

                ModelState.AddModelError("", "Usuario o contraseña incorrectos");
                return View(vm);
            }

            // Login exitoso → reset preventivo de contador y baneo
            usuario.IntentosFallidos = 0;
            usuario.FechaBaneo       = null;

            // Generar token de sesión único → invalida cualquier sesión anterior.
            // Si por algún motivo el UPDATE falla (ej: columna SessionToken ausente
            // en una BD con esquema desactualizado), no bloqueamos el login: el
            // middleware de validación tolera el desajuste dejando pasar el request.
            var sessionToken = GenerarTokenSeguro();
            try
            {
                usuario.SessionToken = sessionToken;
                await _db.SaveChangesAsync();
            }
            catch
            {
                _db.Entry(usuario).State = EntityState.Unchanged;
            }

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

            // IsPersistent controla si la cookie sobrevive al cierre del navegador.
            // Si el usuario no marca "Recordarme", la cookie se borra al cerrar
            // el navegador (útil en dispositivos compartidos).
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = vm.Recordarme });

            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return (usuario.Rol == "Admin" || usuario.Rol == "SuperAdmin")
                ? RedirectToAction("Index", "Admin")
                : RedirectToAction("Index", "Examen");
        }

        // ── Rate-limit del endpoint Verificar por IP ──────────────────
        // 30 requests por minuto es suficiente para un formulario humano,
        // pero frena la enumeración masiva de correos/DNIs/celulares.
        private static readonly ConcurrentDictionary<string, (int count, DateTime windowStart)> _verificarThrottle = new();
        private const int VerificarMaxPorMinuto = 30;

        // ── POST /Account/Verificar ──────────────────────────────────
        // Antes era GET — se cambió a POST con token CSRF para evitar
        // enumeración masiva desde otros sitios o por scraping.
        /// <summary>
        /// Verifica en tiempo real si un campo único (usuario, correo, celular o DNI)
        /// ya está en uso. Usado por AJAX durante el registro para dar feedback inmediato.
        /// Responde con <c>{ ocupado: bool }</c>.
        /// </summary>
        /// <param name="campo">Nombre del campo a verificar: "usuario", "correo", "celular" o "dni".</param>
        /// <param name="valor">Valor a comprobar contra la base de datos.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verificar(string campo, string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return Json(new { ocupado = false });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ahora = DateTime.UtcNow;
            var entry = _verificarThrottle.AddOrUpdate(ip,
                _ => (1, ahora),
                (_, prev) => (ahora - prev.windowStart) > TimeSpan.FromMinutes(1)
                    ? (1, ahora)
                    : (prev.count + 1, prev.windowStart));

            if (entry.count > VerificarMaxPorMinuto)
                return StatusCode(429, new { ocupado = false, error = "Demasiadas solicitudes." });

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
        /// <summary>
        /// Muestra el formulario de auto-registro de estudiantes.
        /// Carga los tipos de examen activos para que el usuario elija su modalidad.
        /// </summary>
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
        /// <summary>
        /// Crea un nuevo estudiante a partir del formulario de registro.
        /// Valida unicidad de correo, celular y DNI; genera el nombre de usuario
        /// automáticamente (NOMBRE.APELLIDO con fallbacks si hay colisión);
        /// hashea la contraseña con BCrypt; asigna el tipo de examen elegido;
        /// y envía un correo de verificación.
        /// </summary>
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

            // ── Generar username automáticamente desde el servidor ───
            var primerN     = vm.PrimerNombre.Trim().Split(' ')[0].ToUpperInvariant();
            var primerA     = vm.PrimerApellido.Trim().Split(' ')[0].ToUpperInvariant();
            var usernameBase = $"{primerN}.{primerA}";
            string nombreUpper;

            if (!await _db.Usuarios.AnyAsync(u => u.NombreUsuario == usernameBase))
            {
                // Opción 1: NOMBRE.APELLIDO disponible
                nombreUpper = usernameBase;
            }
            else if (!string.IsNullOrWhiteSpace(vm.SegundoNombre))
            {
                // Opción 2: SEGUNDONOMBRE.APELLIDO
                var segundoN = vm.SegundoNombre.Trim().Split(' ')[0].ToUpperInvariant();
                var fallback = $"{segundoN}.{primerA}";
                if (!await _db.Usuarios.AnyAsync(u => u.NombreUsuario == fallback))
                {
                    nombreUpper = fallback;
                }
                else
                {
                    // Opción 3: NOMBRE.APELLIDO2, NOMBRE.APELLIDO3…
                    int i = 2;
                    do { nombreUpper = $"{usernameBase}{i++}"; }
                    while (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUpper) && i < 100);
                }
            }
            else
            {
                // Opción 3 directa: NOMBRE.APELLIDO2, NOMBRE.APELLIDO3…
                int i = 2;
                nombreUpper = usernameBase;
                do { nombreUpper = $"{usernameBase}{i++}"; }
                while (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUpper) && i < 100);
            }

            var tokenVerificacion = GenerarTokenSeguro();

            var nuevoUsuario = new Estudiante
            {
                NombreUsuario          = nombreUpper,
                Correo                 = vm.Correo.Trim(),
                Contrasena             = BCrypt.Net.BCrypt.HashPassword(vm.Contrasena),
                Rol                    = "Usuario",
                FechaCreacion          = DateTime.Now,
                Activo                 = true,
                EsTrial                = true,
                EmailVerificado        = true,
                EmailVerificacionToken = null,
                PrimerNombre           = vm.PrimerNombre.Trim().ToUpperInvariant(),
                SegundoNombre          = string.IsNullOrWhiteSpace(vm.SegundoNombre) ? null : vm.SegundoNombre.Trim().ToUpperInvariant(),
                PrimerApellido         = vm.PrimerApellido.Trim().ToUpperInvariant(),
                SegundoApellido        = string.IsNullOrWhiteSpace(vm.SegundoApellido) ? null : vm.SegundoApellido.Trim().ToUpperInvariant(),
                Celular                = vm.Celular.Trim(),
                Dni                    = vm.Dni.Trim()
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

            // Enviar email de verificación
            var linkVerificacion = Url.Action("VerificarEmail", "Account",
                new { token = tokenVerificacion }, Request.Scheme);

            var cuerpoVerif = $@"
<div style='font-family:Inter,sans-serif;max-width:520px;margin:auto;padding:24px'>
  <h2 style='color:#0d6efd'>Simulacro SERUMS</h2>
  <p>Hola <strong>{nuevoUsuario.NombreUsuario}</strong>, ¡bienvenido!</p>
  <p>Gracias por registrarte. Solo falta un paso: verifica tu correo electrónico.</p>
  <p style='text-align:center;margin:32px 0'>
    <a href='{linkVerificacion}'
       style='background:#198754;color:#fff;padding:14px 28px;
              border-radius:8px;text-decoration:none;font-weight:bold'>
      ✓ Verificar mi correo
    </a>
  </p>
  <p style='color:#666;font-size:.9rem'>
    Si no creaste esta cuenta, puedes ignorar este mensaje.
  </p>
  <hr style='border:none;border-top:1px solid #eee;margin:24px 0'/>
  <p style='color:#999;font-size:.8rem'>© {DateTime.Now.Year} Simulacro SERUMS</p>
</div>";

            try { await _email.EnviarAsync(nuevoUsuario.Correo, "Verifica tu correo – Simulacro SERUMS", cuerpoVerif); }
            catch (Exception ex) { _log.LogError(ex, "No se pudo enviar correo de verificación a {Correo}", nuevoUsuario.Correo); }

            TempData["CorreoEnviado"] = nuevoUsuario.Correo;
            return RedirectToAction(nameof(VerificacionPendiente));
        }

        // ── POST /Account/Logout ─────────────────────────────────────
        // Solo POST con token CSRF: evita que un tercero pueda forzar el logout
        // mediante <img src="/Account/Logout"> o similares.
        /// <summary>
        /// Cierra la sesión del usuario eliminando la cookie de autenticación.
        /// Solo acepta POST para proteger contra ataques de logout por referencia cruzada (CSRF).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ── GET /Account/AccesoDenegado ──────────────────────────────
        /// <summary>
        /// Vista de acceso denegado. ASP.NET Core redirige aquí automáticamente
        /// cuando un usuario autenticado intenta acceder a un recurso para el
        /// que no tiene el rol requerido.
        /// </summary>
        public IActionResult AccesoDenegado() => View();

        // ── GET /Account/OlvideMiContrasena ──────────────────────────
        /// <summary>Muestra el formulario para solicitar el restablecimiento de contraseña.</summary>
        [HttpGet]
        public IActionResult OlvideMiContrasena() => View();

        // ── POST /Account/OlvideMiContrasena ─────────────────────────
        /// <summary>
        /// Genera un token de restablecimiento con expiración de 1 hora y envía
        /// el enlace al correo del usuario. Siempre muestra el mismo mensaje de
        /// éxito, sin revelar si el correo existe en el sistema (previene enumeración).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OlvideMiContrasena(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                ModelState.AddModelError("", "Ingresa tu correo electrónico.");
                return View();
            }

            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == correo.Trim() && u.Activo);

            // Siempre mostrar el mismo mensaje (no revelar si el correo existe)
            TempData["Exito"] = "Si el correo está registrado, recibirás un enlace en breve.";

            if (usuario != null)
            {
                var token  = GenerarTokenSeguro();
                usuario.PasswordResetToken  = token;
                usuario.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
                await _db.SaveChangesAsync();

                var link = Url.Action("ResetearContrasena", "Account",
                    new { token }, Request.Scheme);

                var cuerpo = $@"
<div style='font-family:Inter,sans-serif;max-width:520px;margin:auto;padding:24px'>
  <h2 style='color:#0d6efd'>Simulacro SERUMS</h2>
  <p>Hola <strong>{usuario.NombreUsuario}</strong>,</p>
  <p>Recibimos una solicitud para restablecer tu contraseña.</p>
  <p style='text-align:center;margin:32px 0'>
    <a href='{link}'
       style='background:#0d6efd;color:#fff;padding:14px 28px;
              border-radius:8px;text-decoration:none;font-weight:bold'>
      Restablecer contraseña
    </a>
  </p>
  <p style='color:#666;font-size:.9rem'>
    Este enlace expira en <strong>1 hora</strong>. Si no solicitaste esto, ignora este mensaje.
  </p>
  <hr style='border:none;border-top:1px solid #eee;margin:24px 0'/>
  <p style='color:#999;font-size:.8rem'>© {DateTime.Now.Year} Simulacro SERUMS</p>
</div>";

                try { await _email.EnviarAsync(usuario.Correo, "Restablecer contraseña – Simulacro SERUMS", cuerpo); }
                catch (Exception ex) { _log.LogError(ex, "No se pudo enviar correo de recuperación a {Correo}", usuario.Correo); }
            }

            return RedirectToAction(nameof(Login));
        }

        // ── GET /Account/ResetearContrasena ──────────────────────────
        /// <summary>
        /// Valida el token de restablecimiento (debe existir en BD y no haber expirado)
        /// y, si es válido, muestra el formulario para introducir la nueva contraseña.
        /// </summary>
        /// <param name="token">Token hexadecimal incluido en el enlace del email.</param>
        [HttpGet]
        public async Task<IActionResult> ResetearContrasena(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(Login));

            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token
                                       && u.PasswordResetExpiry > DateTime.UtcNow);

            if (usuario == null)
            {
                TempData["Error"] = "El enlace es inválido o ya expiró. Solicita uno nuevo.";
                return RedirectToAction(nameof(OlvideMiContrasena));
            }

            ViewBag.Token = token;
            return View();
        }

        // ── POST /Account/ResetearContrasena ─────────────────────────
        /// <summary>
        /// Aplica la nueva contraseña (hasheada con BCrypt), invalida el token de reset
        /// y anula el SessionToken activo para forzar re-login en todos los dispositivos.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetearContrasena(string token, string nuevaContrasena, string confirmar)
        {
            ViewBag.Token = token;

            if (string.IsNullOrWhiteSpace(nuevaContrasena) || nuevaContrasena.Length < 6)
            {
                ModelState.AddModelError("", "La contraseña debe tener al menos 6 caracteres.");
                return View();
            }
            if (nuevaContrasena != confirmar)
            {
                ModelState.AddModelError("", "Las contraseñas no coinciden.");
                return View();
            }

            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token
                                       && u.PasswordResetExpiry > DateTime.UtcNow);

            if (usuario == null)
            {
                TempData["Error"] = "El enlace es inválido o ya expiró.";
                return RedirectToAction(nameof(OlvideMiContrasena));
            }

            usuario.Contrasena          = BCrypt.Net.BCrypt.HashPassword(nuevaContrasena);
            usuario.PasswordResetToken  = null;
            usuario.PasswordResetExpiry = null;
            // Invalidar sesión activa
            usuario.SessionToken = null;
            await _db.SaveChangesAsync();

            TempData["Exito"] = "Contraseña restablecida correctamente. Inicia sesión.";
            return RedirectToAction(nameof(Login));
        }

        // ── GET /Account/VerificacionPendiente ───────────────────────
        [HttpGet]
        public IActionResult VerificacionPendiente()
        {
            ViewBag.Correo = TempData["CorreoEnviado"] as string;
            return View();
        }

        // ── GET /Account/VerificarEmail?token=... ────────────────────
        [HttpGet]
        public async Task<IActionResult> VerificarEmail(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(Login));

            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.EmailVerificacionToken == token);

            if (usuario == null)
            {
                TempData["Error"] = "El enlace de verificación es inválido o ya fue usado.";
                return RedirectToAction(nameof(Login));
            }

            usuario.EmailVerificado        = true;
            usuario.EmailVerificacionToken = null;
            await _db.SaveChangesAsync();

            TempData["Exito"] = "¡Correo verificado! Ya puedes iniciar sesión.";
            return RedirectToAction(nameof(Login));
        }

        // ── POST /Account/ReenviarVerificacion ───────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReenviarVerificacion(string correo)
        {
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == correo.Trim() && !u.EmailVerificado);

            if (usuario != null)
            {
                // Regenerar token
                var token = GenerarTokenSeguro();
                usuario.EmailVerificacionToken = token;
                await _db.SaveChangesAsync();

                var link = Url.Action("VerificarEmail", "Account", new { token }, Request.Scheme);

                var cuerpo = $@"
<div style='font-family:Inter,sans-serif;max-width:520px;margin:auto;padding:24px'>
  <h2 style='color:#0d6efd'>Simulacro SERUMS</h2>
  <p>Hola <strong>{usuario.NombreUsuario}</strong>,</p>
  <p>Aquí tienes tu nuevo enlace de verificación:</p>
  <p style='text-align:center;margin:32px 0'>
    <a href='{link}'
       style='background:#198754;color:#fff;padding:14px 28px;
              border-radius:8px;text-decoration:none;font-weight:bold'>
      ✓ Verificar mi correo
    </a>
  </p>
  <hr style='border:none;border-top:1px solid #eee;margin:24px 0'/>
  <p style='color:#999;font-size:.8rem'>© {DateTime.Now.Year} Simulacro SERUMS</p>
</div>";

                try { await _email.EnviarAsync(usuario.Correo, "Verifica tu correo – Simulacro SERUMS", cuerpo); }
                catch (Exception ex) { _log.LogError(ex, "No se pudo reenviar correo de verificación a {Correo}", usuario.Correo); }
            }

            TempData["CorreoEnviado"] = correo;
            TempData["Reenviado"]     = true;
            return RedirectToAction(nameof(VerificacionPendiente));
        }

        private IActionResult RedirectToRol()
        {
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
                return RedirectToAction("Index", "Admin");

            return RedirectToAction("Index", "Examen");
        }
    }
}
