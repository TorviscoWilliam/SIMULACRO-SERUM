using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
using SimulacroExamen.ViewModels;

namespace SimulacroExamen.Controllers
{
    /// <summary>
    /// Panel de administración central. Gestiona usuarios, preguntas, tipos de examen,
    /// noticias, planes de suscripción, anuncios globales, estadísticas y configuración
    /// de correo. Accesible únicamente por los roles <c>Admin</c> y <c>SuperAdmin</c>.
    /// </summary>
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController : BaseController
    {
        private readonly ApplicationDbContext     _db;
        private readonly IExcelService            _excel;
        private readonly IWebHostEnvironment      _env;
        private readonly IEmailService            _email;
        private readonly IConfiguration           _config;
        private readonly ILogger<AdminController> _log;
        private readonly ISecretProtector         _secretProtector;

        /// <summary>
        /// Inyecta todas las dependencias necesarias para el panel admin.
        /// </summary>
        public AdminController(ApplicationDbContext db, IExcelService excel,
                               IWebHostEnvironment env, IEmailService email,
                               IConfiguration config, ILogger<AdminController> log,
                               ISecretProtector secretProtector)
        {
            _db              = db;
            _excel           = excel;
            _env             = env;
            _email           = email;
            _config          = config;
            _log             = log;
            _secretProtector = secretProtector;
        }

        // ── Dashboard ────────────────────────────────────────────────
        /// <summary>
        /// Vista principal del panel de administración.
        /// Muestra estadísticas globales (totales de usuarios, preguntas y exámenes),
        /// métricas del día filtrado (tasa de aprobación, mejor puntaje), gráfico de
        /// barras de exámenes de los últimos 7 días, gráfico de dona por tipo de examen
        /// y un ranking Top-10 filtrable por fecha y nombre de usuario.
        /// </summary>
        /// <param name="fecha">Fecha a filtrar; si es null se usa la fecha actual.</param>
        /// <param name="usuario">Fragmento de nombre de usuario para filtrar el ranking.</param>
        public async Task<IActionResult> Index(DateTime? fecha, string? usuario)
        {
            var hoy    = DateTime.Today;
            var filtro = fecha.HasValue ? fecha.Value.Date : hoy;
            var hace7  = hoy.AddDays(-6);

            // Guardar filtros activos para la vista
            ViewBag.FiltroFecha   = filtro.ToString("yyyy-MM-dd");   // formato input[type=date]
            ViewBag.FiltroUsuario = usuario?.Trim() ?? string.Empty;
            ViewBag.FiltroFechaStr = filtro.ToString("dd/MM/yyyy");
            ViewBag.EsFiltrado    = filtro != hoy || !string.IsNullOrWhiteSpace(usuario);

            // ── Stats generales (sin filtro) ─────────────────────────
            ViewBag.TotalUsuarios  = await _db.Estudiantes.CountAsync(u => u.Activo);
            ViewBag.TotalAdmins    = await _db.Administradores.CountAsync(u => u.Activo);
            ViewBag.TotalPreguntas = await _db.Preguntas.CountAsync(p => p.Activo);
            ViewBag.TotalExamenes  = await _db.Examenes.CountAsync(e => e.Completado);
            ViewBag.PromedioGlobal = await _db.Examenes
                .Where(e => e.Completado && e.TotalPreguntas > 0)
                .AverageAsync(e => (double?)((double)e.Puntaje / e.TotalPreguntas * 100)) ?? 0;

            // ── Stats del día filtrado ───────────────────────────────
            ViewBag.ExamenesHoy = await _db.Examenes
                .CountAsync(e => e.Completado && e.FechaFin.HasValue
                              && e.FechaFin.Value.Date == filtro);

            ViewBag.UsuariosActivosHoy = await _db.Examenes
                .Where(e => e.Completado && e.FechaFin.HasValue && e.FechaFin.Value.Date == filtro)
                .Select(e => e.UsuarioId)
                .Distinct()
                .CountAsync();

            var totalFiltro = await _db.Examenes.CountAsync(
                e => e.Completado && e.FechaFin.HasValue
                  && e.FechaFin.Value.Date == filtro && e.TotalPreguntas > 0);
            var aprobadosFiltro = await _db.Examenes.CountAsync(
                e => e.Completado && e.FechaFin.HasValue && e.FechaFin.Value.Date == filtro
                  && e.TotalPreguntas > 0 && (double)e.Puntaje / e.TotalPreguntas >= 0.60);
            ViewBag.TasaAprobacionHoy = totalFiltro > 0
                ? Math.Round((double)aprobadosFiltro / totalFiltro * 100, 1) : 0.0;

            var mejorFiltro = await _db.Examenes
                .Where(e => e.Completado && e.FechaFin.HasValue && e.FechaFin.Value.Date == filtro)
                .MaxAsync(e => (int?)e.Puntaje) ?? 0;
            ViewBag.MejorPuntajeHoyVigesimal = Math.Round(mejorFiltro * 0.2, 2);

            // ── Gráfico de barras: exámenes últimos 7 días (siempre desde hoy) ──
            var examenesPorDia = await _db.Examenes
                .Where(e => e.Completado && e.FechaFin.HasValue && e.FechaFin.Value >= hace7)
                .GroupBy(e => e.FechaFin!.Value.Date)
                .Select(g => new { Fecha = g.Key, Cantidad = g.Count() })
                .ToListAsync();

            var dias = Enumerable.Range(0, 7).Select(i => hoy.AddDays(-6 + i)).ToList();
            ViewBag.ChartLabels = dias.Select(d => d.ToString("dd/MM")).ToArray();
            ViewBag.ChartData   = dias
                .Select(d => examenesPorDia.FirstOrDefault(x => x.Fecha == d)?.Cantidad ?? 0)
                .ToArray();

            // ── Gráfico de dona: preguntas por tipo ──────────────────
            var pregsPorTipo = await _db.Preguntas
                .Where(p => p.Activo && p.TipoExamenId != null)
                .GroupBy(p => p.TipoExamen!.Nombre)
                .Select(g => new { Tipo = g.Key, Cantidad = g.Count() })
                .OrderByDescending(x => x.Cantidad)
                .Take(10)
                .ToListAsync();

            ViewBag.TipoLabels = pregsPorTipo.Select(x => x.Tipo).ToArray();
            ViewBag.TipoCounts = pregsPorTipo.Select(x => x.Cantidad).ToArray();

            // ── Top 10 con filtro de fecha y/o usuario ────────────────
            var rankingQuery = _db.Examenes
                .Where(e => e.Completado && e.FechaFin.HasValue
                         && e.FechaFin.Value.Date == filtro && e.TotalPreguntas > 0)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(usuario))
            {
                var u = usuario.Trim().ToUpperInvariant();
                rankingQuery = rankingQuery.Where(e => e.Usuario.NombreUsuario.Contains(u));
            }

            var rankingRaw = await rankingQuery
                .Include(e => e.Usuario)
                .Include(e => e.TipoExamen)
                .OrderByDescending(e => e.Puntaje)
                .ThenBy(e => e.FechaFin)
                .Select(e => new TopRankingVM
                {
                    NombreUsuario    = e.Usuario.NombreUsuario,
                    TipoExamen       = e.TipoExamen != null ? e.TipoExamen.Nombre : "Sin tipo",
                    Puntaje          = e.Puntaje,
                    TotalPreguntas   = e.TotalPreguntas,
                    PuntajeVigesimal = e.TotalPreguntas > 0 ? Math.Round((double)e.Puntaje / e.TotalPreguntas * 20, 2) : 0,
                    Porcentaje       = Math.Round((double)e.Puntaje / e.TotalPreguntas * 100, 1),
                    FechaFin         = e.FechaFin!.Value
                })
                .ToListAsync();

            ViewBag.RankingPorTipo = rankingRaw
                .GroupBy(r => r.TipoExamen)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(r => r.Puntaje).ThenBy(r => r.FechaFin).Take(10).ToList()
                );

            ViewBag.RankingGeneral = rankingRaw
                .OrderByDescending(r => r.Puntaje)
                .ThenBy(r => r.FechaFin)
                .Take(10)
                .ToList();

            return View();
        }

        // ═══════════════════════════════════════════════════════════
        //  USUARIOS
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Lista paginada de todos los usuarios del sistema (estudiantes y administradores).
        /// Enriquece cada registro con el nombre del plan de suscripción asignado para
        /// evitar N+1 queries: carga los planes en un único query adicional.
        /// </summary>
        /// <param name="page">Número de página (base 1).</param>
        public async Task<IActionResult> Usuarios(int page = 1)
        {
            const int pageSize = 15;

            var total = await _db.Usuarios.CountAsync();

            // Cargamos las entidades en memoria para poder acceder a campos
            // polimórficos (EsTrial, FechaVencimiento, IntentosExtra, PlanSuscripcionId) de Estudiante.
            var raw = await _db.Usuarios
                .Include(u => u.Examenes)
                .Include(u => u.UsuariosTipoExamen).ThenInclude(ut => ut.TipoExamen)
                .OrderByDescending(u => u.FechaCreacion)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Carga los nombres de planes para los estudiantes de esta página (1 query extra)
            var planIds = raw.OfType<Estudiante>()
                .Where(e => e.PlanSuscripcionId.HasValue)
                .Select(e => e.PlanSuscripcionId!.Value)
                .Distinct().ToList();
            var planesNombre = planIds.Any()
                ? await _db.PlanesSuscripcion
                    .Where(p => planIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Nombre)
                : new Dictionary<int, string>();

            var usuarios = raw.Select(u =>
            {
                var est = u as Estudiante;
                return new UsuarioListaViewModel
                {
                    Id               = u.Id,
                    NombreUsuario    = u.NombreUsuario,
                    Correo           = u.Correo,
                    Rol              = u.Rol,
                    FechaCreacion    = u.FechaCreacion,
                    Activo           = u.Activo,
                    TotalExamenes    = u.Examenes.Count(e => e.Completado),
                    MejorPuntaje     = u.Examenes.Any(e => e.Completado)
                        ? u.Examenes.Where(e => e.Completado).Max(e => e.Puntaje)
                        : 0,
                    TiposAsignados   = u.UsuariosTipoExamen
                        .Select(ut => ut.TipoExamen.Nombre).ToList(),
                    IntentosExtra    = est?.IntentosExtra ?? 0,
                    NombreCompleto   = u.NombreCompleto.Length > 0 ? u.NombreCompleto : null,
                    Celular          = u.Celular,
                    Dni              = u.Dni,
                    EsTrial          = est?.EsTrial ?? false,
                    FechaVencimiento = est?.FechaVencimiento,
                    PlanSuscripcionId = est?.PlanSuscripcionId,
                    PlanNombre       = est?.PlanSuscripcionId.HasValue == true
                        ? planesNombre.GetValueOrDefault(est.PlanSuscripcionId.Value)
                        : null
                };
            }).ToList();

            ViewBag.Page             = page;
            ViewBag.PageSize         = pageSize;
            ViewBag.TotalItems       = total;
            ViewBag.TotalPages       = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.PlanesDisponibles = await _db.PlanesSuscripcion
                .Where(p => p.Activo).OrderBy(p => p.Orden)
                .Select(p => new { p.Id, p.Nombre }).ToListAsync();

            return View(usuarios);
        }

        // POST /Admin/AjustarIntentos
        /// <summary>
        /// Establece la cantidad de intentos de examen diarios extra para un estudiante.
        /// El total de intentos diarios efectivos es 5 (base) + <paramref name="intentosExtra"/>.
        /// Solo aplica a estudiantes; retorna error si el usuario es un administrador.
        /// </summary>
        /// <param name="id">ID del usuario a modificar.</param>
        /// <param name="intentosExtra">Intentos adicionales (mínimo 0).</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjustarIntentos(int id, int intentosExtra)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u is not Estudiante est)
            {
                TempData["Error"] = "Solo se pueden ajustar intentos para estudiantes.";
                return RedirectToAction(nameof(Usuarios));
            }

            est.IntentosExtra = Math.Max(0, intentosExtra);
            await _db.SaveChangesAsync();

            TempData["Exito"] = $"Intentos diarios de '{est.NombreUsuario}' actualizados a {5 + est.IntentosExtra}.";
            return RedirectToAction(nameof(Usuarios));
        }

        /// <summary>Muestra el formulario para crear un nuevo usuario manualmente.</summary>
        public IActionResult CrearUsuario() => View(new CrearUsuarioViewModel());

        /// <summary>
        /// Crea un nuevo usuario (estudiante o administrador) desde el panel admin.
        /// Valida unicidad de nombre de usuario y correo, aplica la jerarquía de roles
        /// (un Admin no puede crear SuperAdmins) y hashea la contraseña con BCrypt.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearUsuario(CrearUsuarioViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // Guardar nombre en MAYÚSCULAS
            var nombreUpper = vm.NombreUsuario.Trim().ToUpperInvariant();

            if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUpper))
            {
                ModelState.AddModelError(nameof(vm.NombreUsuario), "El nombre de usuario ya existe");
                return View(vm);
            }

            if (await _db.Usuarios.AnyAsync(u => u.Correo == vm.Correo.Trim()))
            {
                ModelState.AddModelError(nameof(vm.Correo), "El correo ya está registrado");
                return View(vm);
            }

            var rolesPermitidos = EsSuperAdmin
                ? new[] { "SuperAdmin", "Admin", "Usuario" }
                : new[] { "Admin", "Usuario" };
            if (!rolesPermitidos.Contains(vm.Rol))
            {
                ModelState.AddModelError(nameof(vm.Rol), "Rol inválido");
                return View(vm);
            }

            Usuario nuevoUsuario = vm.Rol == "Usuario"
                ? new Estudiante
                {
                    EsTrial = vm.EsTrial
                }
                : new Administrador();

            nuevoUsuario.NombreUsuario   = nombreUpper;
            nuevoUsuario.Correo          = vm.Correo.Trim();
            nuevoUsuario.Contrasena      = BCrypt.Net.BCrypt.HashPassword(vm.Contrasena);
            nuevoUsuario.Rol             = vm.Rol;
            nuevoUsuario.FechaCreacion   = DateTime.Now;
            nuevoUsuario.Activo          = true;
            nuevoUsuario.PrimerNombre    = string.IsNullOrWhiteSpace(vm.PrimerNombre)    ? null : vm.PrimerNombre.Trim();
            nuevoUsuario.SegundoNombre   = string.IsNullOrWhiteSpace(vm.SegundoNombre)   ? null : vm.SegundoNombre.Trim();
            nuevoUsuario.PrimerApellido  = string.IsNullOrWhiteSpace(vm.PrimerApellido)  ? null : vm.PrimerApellido.Trim();
            nuevoUsuario.SegundoApellido = string.IsNullOrWhiteSpace(vm.SegundoApellido) ? null : vm.SegundoApellido.Trim();
            nuevoUsuario.Celular         = string.IsNullOrWhiteSpace(vm.Celular)         ? null : vm.Celular.Trim();
            nuevoUsuario.Dni             = string.IsNullOrWhiteSpace(vm.Dni)             ? null : vm.Dni.Trim();

            _db.Usuarios.Add(nuevoUsuario);

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Usuario '{nombreUpper}' creado correctamente.";
            return RedirectToAction(nameof(Usuarios));
        }

        // GET /Admin/EditarUsuario/{id}
        /// <summary>
        /// Carga el formulario de edición de un usuario existente.
        /// Bloquea la edición de cuentas SuperAdmin para usuarios que no tengan ese rol.
        /// Carga también los planes de suscripción disponibles para mostrarlos en el select.
        /// </summary>
        /// <param name="id">ID del usuario a editar.</param>
        public async Task<IActionResult> EditarUsuario(int id)
        {
            var usuario = await _db.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            // Solo el SuperAdmin puede editar a otro SuperAdmin
            if (usuario.Rol == "SuperAdmin" && !EsSuperAdmin)
            {
                TempData["Error"] = "No tienes permisos para editar al administrador principal.";
                return RedirectToAction(nameof(Usuarios));
            }

            var vm = new EditarUsuarioViewModel
            {
                Id                = usuario.Id,
                NombreUsuario     = usuario.NombreUsuario,
                Correo            = usuario.Correo,
                Rol               = usuario.Rol,
                PrimerNombre      = usuario.PrimerNombre,
                SegundoNombre     = usuario.SegundoNombre,
                PrimerApellido    = usuario.PrimerApellido,
                SegundoApellido   = usuario.SegundoApellido,
                Celular           = usuario.Celular,
                Dni               = usuario.Dni,
                FechaVencimiento  = (usuario as Estudiante)?.FechaVencimiento,
                PlanSuscripcionId = (usuario as Estudiante)?.PlanSuscripcionId
            };

            ViewBag.PlanesDisponibles = await _db.PlanesSuscripcion
                .Where(p => p.Activo).OrderBy(p => p.Orden)
                .Select(p => new { p.Id, p.Nombre }).ToListAsync();

            return View(vm);
        }

        /// <summary>
        /// Persiste los cambios de un usuario editado. Valida unicidad de nombre y correo
        /// excluyendo el propio ID, controla la jerarquía de roles e impide cambiar el
        /// tipo discriminador (Estudiante ↔ Administrador) sin eliminar y recrear el registro.
        /// Solo actualiza la contraseña si se proporcionó una nueva en el formulario.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarUsuario(EditarUsuarioViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.PlanesDisponibles = await _db.PlanesSuscripcion
                    .Where(p => p.Activo).OrderBy(p => p.Orden)
                    .Select(p => new { p.Id, p.Nombre }).ToListAsync();
                return View(vm);
            }

            var usuario = await _db.Usuarios.FindAsync(vm.Id);
            if (usuario == null) return NotFound();

            var nombreUpper = vm.NombreUsuario.Trim().ToUpperInvariant();

            if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUpper && u.Id != vm.Id))
            {
                ModelState.AddModelError(nameof(vm.NombreUsuario), "El nombre de usuario ya existe");
                return View(vm);
            }

            if (await _db.Usuarios.AnyAsync(u => u.Correo == vm.Correo.Trim() && u.Id != vm.Id))
            {
                ModelState.AddModelError(nameof(vm.Correo), "El correo ya está registrado");
                return View(vm);
            }

            var rolesValidos = EsSuperAdmin
                ? new[] { "SuperAdmin", "Admin", "Usuario" }
                : new[] { "Admin", "Usuario" };
            if (!rolesValidos.Contains(vm.Rol))
            {
                ModelState.AddModelError(nameof(vm.Rol), "Rol inválido");
                return View(vm);
            }

            // Solo el SuperAdmin puede editar a otro SuperAdmin
            if (usuario.Rol == "SuperAdmin" && !EsSuperAdmin)
            {
                TempData["Error"] = "No tienes permisos para editar al administrador principal.";
                return RedirectToAction(nameof(Usuarios));
            }

            // Detectar cambio de tipo: Estudiante→Admin o Admin→Estudiante requiere
            // eliminar y recrear el usuario. Se bloquea vía validación de rol.
            bool esEstudianteActual = usuario is Estudiante;
            bool seriaEstudiante    = vm.Rol == "Usuario";
            if (esEstudianteActual != seriaEstudiante)
            {
                ModelState.AddModelError(nameof(vm.Rol),
                    "No se puede cambiar un Estudiante a Administrador o viceversa. " +
                    "Elimina el usuario y créalo de nuevo con el rol correcto.");
                return View(vm);
            }

            usuario.NombreUsuario   = nombreUpper;
            usuario.Correo          = vm.Correo.Trim();
            usuario.Rol             = vm.Rol;
            usuario.PrimerNombre    = string.IsNullOrWhiteSpace(vm.PrimerNombre)    ? null : vm.PrimerNombre.Trim().ToUpperInvariant();
            usuario.SegundoNombre   = string.IsNullOrWhiteSpace(vm.SegundoNombre)   ? null : vm.SegundoNombre.Trim().ToUpperInvariant();
            usuario.PrimerApellido  = string.IsNullOrWhiteSpace(vm.PrimerApellido)  ? null : vm.PrimerApellido.Trim().ToUpperInvariant();
            usuario.SegundoApellido = string.IsNullOrWhiteSpace(vm.SegundoApellido) ? null : vm.SegundoApellido.Trim().ToUpperInvariant();
            usuario.Celular          = string.IsNullOrWhiteSpace(vm.Celular) ? null : vm.Celular.Trim();
            usuario.Dni              = string.IsNullOrWhiteSpace(vm.Dni)    ? null : vm.Dni.Trim();

            if (usuario is Estudiante est)
            {
                est.FechaVencimiento  = vm.FechaVencimiento;
                est.PlanSuscripcionId = vm.PlanSuscripcionId;
            }

            if (!string.IsNullOrWhiteSpace(vm.ContrasenaNueva))
                usuario.Contrasena = BCrypt.Net.BCrypt.HashPassword(vm.ContrasenaNueva);

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Usuario '{nombreUpper}' actualizado correctamente.";
            return RedirectToAction(nameof(Usuarios));
        }

        /// <summary>
        /// Activa o desactiva un usuario. Impide que el admin se desactive a sí mismo
        /// y que un Admin (no SuperAdmin) desactive una cuenta SuperAdmin.
        /// </summary>
        /// <param name="id">ID del usuario a alternar.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUsuario(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();

            // No permitir desactivarse a uno mismo
            var currentId = CurrentUserId;
            if (u.Id == currentId)
            {
                TempData["Error"] = "No puedes desactivar tu propia cuenta.";
                return RedirectToAction(nameof(Usuarios));
            }

            // Solo el SuperAdmin puede desactivar a otro SuperAdmin
            if (u.Rol == "SuperAdmin" && !EsSuperAdmin)
            {
                TempData["Error"] = "No tienes permisos para desactivar al administrador principal.";
                return RedirectToAction(nameof(Usuarios));
            }

            u.Activo = !u.Activo;
            await _db.SaveChangesAsync();

            TempData["Exito"] = u.Activo
                ? $"Usuario '{u.NombreUsuario}' activado."
                : $"Usuario '{u.NombreUsuario}' desactivado.";

            return RedirectToAction(nameof(Usuarios));
        }

        // POST /Admin/ActivarAccesoCompleto/{id}
        /// <summary>
        /// Promueve a un estudiante de modo trial a acceso completo.
        /// Si no tiene fecha de vencimiento o ya venció, establece 30 días desde hoy.
        /// Opcionalmente asigna un plan de suscripción. Registra el cambio en el log de actividad.
        /// </summary>
        /// <param name="id">ID del estudiante a promover.</param>
        /// <param name="planId">ID del plan de suscripción a asignar (opcional).</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivarAccesoCompleto(int id, int? planId)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u is not Estudiante est)
            {
                TempData["Error"] = "Solo se puede activar acceso completo para estudiantes.";
                return RedirectToAction(nameof(Usuarios));
            }

            est.EsTrial = false;
            est.PlanSuscripcionId = planId;

            if (est.FechaVencimiento == null || est.FechaVencimiento < DateTime.Now)
                est.FechaVencimiento = DateTime.Now.AddDays(30);

            await _db.SaveChangesAsync();

            var planNombre = planId.HasValue
                ? (await _db.PlanesSuscripcion.FindAsync(planId.Value))?.Nombre ?? "—"
                : "—";

            await RegistrarLog("ActivarAccesoCompleto",
                $"Estudiante '{est.NombreUsuario}' promovido a acceso completo " +
                $"| Plan: {planNombre} | Vence: {est.FechaVencimiento:dd/MM/yyyy}");
            TempData["Exito"] = $"'{est.NombreUsuario}' tiene acceso completo hasta el {est.FechaVencimiento:dd/MM/yyyy}" +
                                (planId.HasValue ? $" (Plan: {planNombre})." : ".");
            return RedirectToAction(nameof(Usuarios));
        }

        // POST /Admin/ActivarTrial/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivarTrial(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u is not Estudiante est)
            {
                TempData["Error"] = "Solo se puede revertir a trial para estudiantes.";
                return RedirectToAction(nameof(Usuarios));
            }

            est.EsTrial = true;
            est.PlanSuscripcionId = null;
            await _db.SaveChangesAsync();

            await RegistrarLog("ActivarTrial", $"Estudiante '{est.NombreUsuario}' revertido a modo de prueba (trial)");
            TempData["Exito"] = $"'{est.NombreUsuario}' ha sido puesto en modo de prueba (trial).";
            return RedirectToAction(nameof(Usuarios));
        }

        // POST /Admin/EliminarUsuario/{id}  — Solo SuperAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> EliminarUsuario(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();

            var currentId = CurrentUserId;
            if (u.Id == currentId)
            {
                TempData["Error"] = "No puedes eliminar tu propia cuenta.";
                return RedirectToAction(nameof(Usuarios));
            }

            var nombre = u.NombreUsuario;
            _db.Usuarios.Remove(u);
            await _db.SaveChangesAsync();

            await RegistrarLog("EliminarUsuario", $"Usuario '{nombre}' eliminado permanentemente por el SuperAdmin");
            TempData["Exito"] = $"Usuario '{nombre}' eliminado correctamente.";
            return RedirectToAction(nameof(Usuarios));
        }

        public async Task<IActionResult> ExportarUsuarios()
        {
            var raw = await _db.Usuarios
                .Include(u => u.Examenes)
                .Include(u => u.UsuariosTipoExamen).ThenInclude(ut => ut.TipoExamen)
                .OrderBy(u => u.NombreUsuario)
                .ToListAsync();

            // Load plan names for students that have a plan assigned
            var planIds = raw.OfType<Estudiante>()
                .Where(e => e.PlanSuscripcionId.HasValue)
                .Select(e => e.PlanSuscripcionId!.Value)
                .Distinct()
                .ToList();

            var planNombres = planIds.Count > 0
                ? await _db.PlanesSuscripcion
                    .Where(p => planIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Nombre)
                : new Dictionary<int, string>();

            var usuarios = raw.Select(u =>
            {
                var est = u as Estudiante;
                var planId = est?.PlanSuscripcionId;
                return new UsuarioListaViewModel
                {
                    Id            = u.Id,
                    NombreUsuario = u.NombreUsuario,
                    Correo        = u.Correo,
                    Rol           = u.Rol,
                    FechaCreacion = u.FechaCreacion,
                    Activo        = u.Activo,
                    TotalExamenes = u.Examenes.Count(e => e.Completado),
                    MejorPuntaje  = u.Examenes.Any(e => e.Completado)
                        ? u.Examenes.Where(e => e.Completado).Max(e => e.Puntaje)
                        : 0,
                    TiposAsignados   = u.UsuariosTipoExamen.Select(ut => ut.TipoExamen.Nombre).ToList(),
                    EsTrial          = est?.EsTrial ?? false,
                    FechaVencimiento = est?.FechaVencimiento,
                    PlanSuscripcionId = planId,
                    PlanNombre        = planId.HasValue && planNombres.TryGetValue(planId.Value, out var pn) ? pn : null
                };
            }).ToList();

            var bytes    = _excel.ExportarUsuarios(usuarios);
            var filename = $"Usuarios_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                filename);
        }

        // ═══════════════════════════════════════════════════════════
        //  ASIGNAR ACCESO A TIPOS DE EXAMEN
        // ═══════════════════════════════════════════════════════════

        // GET /Admin/AsignarAcceso/{id}
        public async Task<IActionResult> AsignarAcceso(int id)
        {
            var usuario = await _db.Estudiantes
                .Include(u => u.UsuariosTipoExamen)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null) return NotFound();

            var todosLosTipos = await _db.TiposExamen.Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();
            var tiposAsignados = usuario.UsuariosTipoExamen.Select(ut => ut.TipoExamenId).ToHashSet();

            var vm = new AsignarAccesoViewModel
            {
                UsuarioId     = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                Tipos         = todosLosTipos.Select(t => new TipoAccesoItem
                {
                    TipoExamenId = t.Id,
                    Nombre       = t.Nombre,
                    Asignado     = tiposAsignados.Contains(t.Id)
                }).ToList()
            };

            return View(vm);
        }

        // POST /Admin/AsignarAcceso
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarAcceso(int usuarioId, int[] tiposSeleccionados)
        {
            var usuario = await _db.Estudiantes
                .Include(u => u.UsuariosTipoExamen)
                .FirstOrDefaultAsync(u => u.Id == usuarioId);

            if (usuario == null) return NotFound();

            // Eliminar los accesos actuales
            _db.UsuarioTiposExamen.RemoveRange(usuario.UsuariosTipoExamen);

            // Agregar los nuevos accesos seleccionados
            foreach (var tipoId in tiposSeleccionados)
            {
                _db.UsuarioTiposExamen.Add(new UsuarioTipoExamen
                {
                    UsuarioId     = usuarioId,
                    TipoExamenId  = tipoId,
                    FechaAsignacion = DateTime.Now
                });
            }

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Accesos de '{usuario.NombreUsuario}' actualizados correctamente.";
            return RedirectToAction(nameof(Usuarios));
        }

        // ═══════════════════════════════════════════════════════════
        //  PREGUNTAS
        // ═══════════════════════════════════════════════════════════

        // GET /Admin/Preguntas?tipoId=&page=&pageSize=
        public async Task<IActionResult> Preguntas(int? tipoId, int page = 1, int pageSize = 20)
        {
            if (pageSize != 10 && pageSize != 25 && pageSize != 100)
                pageSize = 20;

            var tipos = await _db.TiposExamen.Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();
            ViewBag.Tipos  = tipos;
            ViewBag.TipoId = tipoId;

            var query = _db.Preguntas
                .Where(p => p.Activo)
                .Include(p => p.TipoExamen)
                .Include(p => p.Alternativas)
                .AsQueryable();

            if (tipoId.HasValue)
                query = query.Where(p => p.TipoExamenId == tipoId.Value);

            var total     = await query.CountAsync();
            var preguntas = await query
                .OrderByDescending(p => p.FechaCreacion)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page      = page;
            ViewBag.PageSize  = pageSize;
            ViewBag.TotalItems = total;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View(preguntas);
        }

        // GET /Admin/CrearPregunta
        public async Task<IActionResult> CrearPregunta()
        {
            ViewBag.Tipos = await _db.TiposExamen.Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();
            return View(new PreguntaFormViewModel());
        }

        // POST /Admin/CrearPregunta
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPregunta(PreguntaFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Tipos = await _db.TiposExamen.Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();
                return View(vm);
            }

            var pregunta = new Pregunta
            {
                TextoPregunta = vm.TextoPregunta,
                TipoExamenId  = vm.TipoExamenId,
                FechaCreacion = DateTime.Now,
                Activo        = true
            };

            pregunta.Alternativas.Add(new Alternativa { TextoAlternativa = vm.RespuestaCorrecta, EsCorrecta = true });
            pregunta.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion2,           EsCorrecta = false });

            if (!string.IsNullOrWhiteSpace(vm.Opcion3))
                pregunta.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion3, EsCorrecta = false });

            if (!string.IsNullOrWhiteSpace(vm.Opcion4))
                pregunta.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion4, EsCorrecta = false });

            _db.Preguntas.Add(pregunta);
            await _db.SaveChangesAsync();

            TempData["Exito"] = "Pregunta agregada correctamente.";
            return RedirectToAction(nameof(Preguntas));
        }

        // GET /Admin/EditarPregunta/{id}
        public async Task<IActionResult> EditarPregunta(int id)
        {
            var p = await _db.Preguntas
                .Include(p => p.Alternativas)
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

            if (p == null) return NotFound();

            ViewBag.Tipos = await _db.TiposExamen.Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();

            var correcta   = p.Alternativas.FirstOrDefault(a => a.EsCorrecta);
            var incorrectas = p.Alternativas.Where(a => !a.EsCorrecta).ToList();

            var vm = new EditarPreguntaViewModel
            {
                Id                = p.Id,
                TextoPregunta     = p.TextoPregunta,
                TipoExamenId      = p.TipoExamenId ?? 0,
                RespuestaCorrecta = correcta?.TextoAlternativa ?? "",
                Opcion2           = incorrectas.ElementAtOrDefault(0)?.TextoAlternativa ?? "",
                Opcion3           = incorrectas.ElementAtOrDefault(1)?.TextoAlternativa,
                Opcion4           = incorrectas.ElementAtOrDefault(2)?.TextoAlternativa
            };

            return View(vm);
        }

        // POST /Admin/EditarPregunta
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPregunta(EditarPreguntaViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Tipos = await _db.TiposExamen.Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();
                return View(vm);
            }

            var p = await _db.Preguntas
                .Include(p => p.Alternativas)
                .FirstOrDefaultAsync(p => p.Id == vm.Id && p.Activo);

            if (p == null) return NotFound();

            p.TextoPregunta = vm.TextoPregunta;
            p.TipoExamenId  = vm.TipoExamenId > 0 ? vm.TipoExamenId : null;

            // Reemplazar alternativas
            _db.Alternativas.RemoveRange(p.Alternativas);

            p.Alternativas.Add(new Alternativa { TextoAlternativa = vm.RespuestaCorrecta, EsCorrecta = true });
            p.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion2,           EsCorrecta = false });

            if (!string.IsNullOrWhiteSpace(vm.Opcion3))
                p.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion3, EsCorrecta = false });

            if (!string.IsNullOrWhiteSpace(vm.Opcion4))
                p.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion4, EsCorrecta = false });

            await _db.SaveChangesAsync();
            TempData["Exito"] = "Pregunta actualizada correctamente.";
            return RedirectToAction(nameof(Preguntas));
        }

        // POST /Admin/EliminarPregunta/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPregunta(int id)
        {
            var p = await _db.Preguntas.FindAsync(id);
            if (p == null) return NotFound();

            p.Activo = false;
            await _db.SaveChangesAsync();

            TempData["Exito"] = "Pregunta eliminada.";
            return RedirectToAction(nameof(Preguntas));
        }

        // POST /Admin/CargarPreguntas (importar Excel)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CargarPreguntas(IFormFile archivo, int tipoExamenId)
        {
            if (archivo == null || archivo.Length == 0)
            {
                TempData["Error"] = "Seleccione un archivo Excel válido.";
                return RedirectToAction(nameof(Preguntas));
            }

            const long MaxFileSize = 10 * 1024 * 1024;
            if (archivo.Length > MaxFileSize)
            {
                TempData["Error"] = "El archivo supera el límite de 10 MB.";
                return RedirectToAction(nameof(Preguntas));
            }

            var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls")
            {
                TempData["Error"] = "Solo se permiten archivos Excel (.xlsx o .xls).";
                return RedirectToAction(nameof(Preguntas));
            }

            List<PreguntaFormViewModel> importadas;
            try
            {
                using var stream = archivo.OpenReadStream();
                importadas = _excel.ImportarPreguntas(stream);
            }
            catch
            {
                TempData["Error"] = "Error al procesar el archivo. Verifique el formato.";
                return RedirectToAction(nameof(Preguntas));
            }

            if (importadas.Count == 0)
            {
                TempData["Error"] = "No se encontraron preguntas válidas en el archivo.";
                return RedirectToAction(nameof(Preguntas));
            }

            // Cargar textos existentes en BD para detectar duplicados (normalizado a minúsculas)
            var existentesQuery = _db.Preguntas.Where(p => p.Activo);
            if (tipoExamenId > 0)
                existentesQuery = existentesQuery.Where(p => p.TipoExamenId == tipoExamenId);

            var textosExistentes = (await existentesQuery.Select(p => p.TextoPregunta).ToListAsync())
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet();

            int guardadas   = 0;
            int duplicadas  = 0;

            // También rastrear duplicados dentro del mismo archivo
            var textosEnArchivo = new HashSet<string>();

            foreach (var vm in importadas)
            {
                var textoNorm = vm.TextoPregunta.Trim().ToLowerInvariant();

                if (textosExistentes.Contains(textoNorm) || !textosEnArchivo.Add(textoNorm))
                {
                    duplicadas++;
                    continue;
                }

                var pregunta = new Pregunta
                {
                    TextoPregunta = vm.TextoPregunta,
                    TipoExamenId  = tipoExamenId > 0 ? tipoExamenId : null,
                    FechaCreacion = DateTime.Now,
                    Activo        = true
                };

                pregunta.Alternativas.Add(new Alternativa { TextoAlternativa = vm.RespuestaCorrecta, EsCorrecta = true });
                pregunta.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion2,           EsCorrecta = false });

                if (!string.IsNullOrWhiteSpace(vm.Opcion3))
                    pregunta.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion3, EsCorrecta = false });

                if (!string.IsNullOrWhiteSpace(vm.Opcion4))
                    pregunta.Alternativas.Add(new Alternativa { TextoAlternativa = vm.Opcion4, EsCorrecta = false });

                _db.Preguntas.Add(pregunta);
                guardadas++;
            }

            await _db.SaveChangesAsync();

            if (duplicadas > 0)
                TempData["Advertencia"] = $"{duplicadas} pregunta(s) omitida(s) por estar duplicadas.";

            if (guardadas > 0)
                TempData["Exito"] = $"Se importaron {guardadas} pregunta(s) correctamente.";
            else
                TempData["Error"] = "No se importó ninguna pregunta nueva (todas eran duplicadas).";

            return RedirectToAction(nameof(Preguntas));
        }

        public IActionResult DescargarPlantilla()
        {
            var bytes = _excel.GenerarPlantillaPreguntas();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Plantilla_Preguntas.xlsx");
        }

        // GET /Admin/ExportarPreguntas?tipoId=
        public async Task<IActionResult> ExportarPreguntas(int? tipoId)
        {
            var query = _db.Preguntas
                .Where(p => p.Activo)
                .Include(p => p.TipoExamen)
                .Include(p => p.Alternativas)
                .AsQueryable();

            if (tipoId.HasValue)
                query = query.Where(p => p.TipoExamenId == tipoId.Value);

            var preguntas = await query.OrderBy(p => p.TipoExamen!.Nombre).ThenBy(p => p.Id).ToListAsync();

            var bytes = _excel.ExportarPreguntas(preguntas);
            var tipo  = tipoId.HasValue
                ? (await _db.TiposExamen.FindAsync(tipoId.Value))?.Nombre ?? "Tipo"
                : "Todos";

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Preguntas_{tipo}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // POST /Admin/EliminarPreguntasEnMasa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPreguntasEnMasa([FromForm] int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No se seleccionaron preguntas.";
                return RedirectToAction(nameof(Preguntas));
            }

            var preguntas = await _db.Preguntas
                .Where(p => ids.Contains(p.Id) && p.Activo)
                .ToListAsync();

            foreach (var p in preguntas)
                p.Activo = false;

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"{preguntas.Count} pregunta(s) eliminada(s).";
            return RedirectToAction(nameof(Preguntas));
        }

        // POST /Admin/AsignarTipoEnMasa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarTipoEnMasa([FromForm] int[] ids, [FromForm] int nuevoTipoId)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No se seleccionaron preguntas.";
                return RedirectToAction(nameof(Preguntas));
            }

            var preguntas = await _db.Preguntas
                .Where(p => ids.Contains(p.Id) && p.Activo)
                .ToListAsync();

            int? tipoDestino = nuevoTipoId > 0 ? nuevoTipoId : null;
            foreach (var p in preguntas)
                p.TipoExamenId = tipoDestino;

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"{preguntas.Count} pregunta(s) reasignada(s).";
            return RedirectToAction(nameof(Preguntas));
        }

        // ═══════════════════════════════════════════════════════════
        //  TIPOS DE EXAMEN
        // ═══════════════════════════════════════════════════════════

        public async Task<IActionResult> TiposExamen()
        {
            var tipos = await _db.TiposExamen
                .OrderBy(t => t.Nombre)
                .ToListAsync();
            try
            {
                var ids     = tipos.Select(t => t.Id).ToList();
                var opciones = await _db.OpcionesDuracion
                    .Where(o => ids.Contains(o.TipoExamenId))
                    .ToListAsync();
                foreach (var t in tipos)
                    t.OpcionesDuracion = opciones.Where(o => o.TipoExamenId == t.Id).ToList();
            }
            catch { /* tabla aún no existe */ }
            return View(tipos);
        }

        public IActionResult CrearTipo() => View(new TipoExamen());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearTipo(TipoExamen tipo)
        {
            if (!ModelState.IsValid) return View(tipo);

            if (await _db.TiposExamen.AnyAsync(t => t.Nombre == tipo.Nombre))
            {
                ModelState.AddModelError(nameof(tipo.Nombre), "Ya existe un tipo con ese nombre.");
                return View(tipo);
            }

            tipo.Activo = true;
            _db.TiposExamen.Add(tipo);
            await _db.SaveChangesAsync();

            TempData["Exito"] = $"Tipo '{tipo.Nombre}' creado correctamente.";
            return RedirectToAction(nameof(TiposExamen));
        }

        public async Task<IActionResult> EditarTipo(int id)
        {
            var tipo = await _db.TiposExamen.FindAsync(id);
            if (tipo == null) return NotFound();
            try
            {
                tipo.OpcionesDuracion = await _db.OpcionesDuracion
                    .Where(o => o.TipoExamenId == id)
                    .OrderBy(o => o.Orden)
                    .ToListAsync();
            }
            catch { /* tabla aún no existe */ }
            return View(tipo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarTipo(TipoExamen tipo)
        {
            if (!ModelState.IsValid)
            {
                try { tipo.OpcionesDuracion = await _db.OpcionesDuracion.Where(o => o.TipoExamenId == tipo.Id).OrderBy(o => o.Orden).ToListAsync(); } catch { }
                return View(tipo);
            }

            if (await _db.TiposExamen.AnyAsync(t => t.Nombre == tipo.Nombre && t.Id != tipo.Id))
            {
                ModelState.AddModelError(nameof(tipo.Nombre), "Ya existe un tipo con ese nombre.");
                try { tipo.OpcionesDuracion = await _db.OpcionesDuracion.Where(o => o.TipoExamenId == tipo.Id).OrderBy(o => o.Orden).ToListAsync(); } catch { }
                return View(tipo);
            }

            var existing = await _db.TiposExamen.FindAsync(tipo.Id);
            if (existing == null) return NotFound();

            existing.Nombre          = tipo.Nombre;
            existing.NumeroPreguntas = tipo.NumeroPreguntas;
            existing.DuracionMinutos = tipo.DuracionMinutos;
            await _db.SaveChangesAsync();

            TempData["Exito"] = $"Tipo '{existing.Nombre}' actualizado correctamente.";
            return RedirectToAction(nameof(EditarTipo), new { id = existing.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarDuracion(int tipoExamenId, string etiqueta,
                                                           int duracionMinutos, int numeroPreguntas = 0)
        {
            var tipo = await _db.TiposExamen.FindAsync(tipoExamenId);
            if (tipo == null) return NotFound();

            if (string.IsNullOrWhiteSpace(etiqueta))
            {
                TempData["Error"] = "La etiqueta es obligatoria.";
                return RedirectToAction(nameof(EditarTipo), new { id = tipoExamenId });
            }

            try
            {
                var orden = await _db.OpcionesDuracion
                    .Where(o => o.TipoExamenId == tipoExamenId)
                    .CountAsync();

                _db.OpcionesDuracion.Add(new OpcionDuracion
                {
                    TipoExamenId    = tipoExamenId,
                    Etiqueta        = etiqueta.Trim(),
                    DuracionMinutos = Math.Max(0, duracionMinutos),
                    NumeroPreguntas = Math.Max(0, numeroPreguntas),
                    Orden           = orden
                });
                await _db.SaveChangesAsync();
                TempData["Exito"] = $"Opción '{etiqueta}' añadida.";
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error guardando opción de duración en tipo {TipoId}", tipoExamenId);
                TempData["Error"] = "No se pudo guardar la opción. Intenta de nuevo.";
            }
            return RedirectToAction(nameof(EditarTipo), new { id = tipoExamenId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarDuracion(int id, string etiqueta,
                                                         int duracionMinutos, int numeroPreguntas = 0)
        {
            int tipoId = 0;
            try
            {
                var opcion = await _db.OpcionesDuracion.FindAsync(id);
                if (opcion == null) return NotFound();
                tipoId = opcion.TipoExamenId;

                if (string.IsNullOrWhiteSpace(etiqueta))
                {
                    TempData["Error"] = "La etiqueta es obligatoria.";
                    return RedirectToAction(nameof(EditarTipo), new { id = tipoId });
                }

                opcion.Etiqueta        = etiqueta.Trim();
                opcion.DuracionMinutos = Math.Max(0, duracionMinutos);
                opcion.NumeroPreguntas = Math.Max(0, numeroPreguntas);
                await _db.SaveChangesAsync();

                TempData["Exito"] = $"Opción '{opcion.Etiqueta}' actualizada.";
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error actualizando opción de duración {Id}", id);
                TempData["Error"] = "No se pudo actualizar. Intenta de nuevo.";
            }
            return RedirectToAction(nameof(EditarTipo), new { id = tipoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoverDuracion(int id, string direccion)
        {
            int tipoId = 0;
            try
            {
                var opcion = await _db.OpcionesDuracion.FindAsync(id);
                if (opcion == null) return NotFound();
                tipoId = opcion.TipoExamenId;

                var hermanas = await _db.OpcionesDuracion
                    .Where(o => o.TipoExamenId == tipoId)
                    .OrderBy(o => o.Orden)
                    .ToListAsync();

                var idx = hermanas.FindIndex(o => o.Id == id);
                var otroIdx = direccion == "up" ? idx - 1 : idx + 1;
                if (idx >= 0 && otroIdx >= 0 && otroIdx < hermanas.Count)
                {
                    var tmp = hermanas[idx].Orden;
                    hermanas[idx].Orden = hermanas[otroIdx].Orden;
                    hermanas[otroIdx].Orden = tmp;
                    await _db.SaveChangesAsync();
                }
            }
            catch { /* tabla aún no existe */ }
            return RedirectToAction(nameof(EditarTipo), new { id = tipoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarDuracion(int id)
        {
            int tipoId = 0;
            try
            {
                var opcion = await _db.OpcionesDuracion.FindAsync(id);
                if (opcion == null) return NotFound();

                tipoId = opcion.TipoExamenId;
                _db.OpcionesDuracion.Remove(opcion);
                await _db.SaveChangesAsync();
                TempData["Exito"] = "Opción de duración eliminada.";
            }
            catch
            {
                TempData["Error"] = "No se pudo eliminar la opción.";
            }
            return RedirectToAction(nameof(EditarTipo), new { id = tipoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTipo(int id)
        {
            var tipo = await _db.TiposExamen.FindAsync(id);
            if (tipo == null) return NotFound();

            tipo.Activo = !tipo.Activo;
            await _db.SaveChangesAsync();

            TempData["Exito"] = tipo.Activo
                ? $"Tipo '{tipo.Nombre}' activado."
                : $"Tipo '{tipo.Nombre}' desactivado.";

            return RedirectToAction(nameof(TiposExamen));
        }

        // ═══════════════════════════════════════════════════════════
        //  NOTICIAS
        // ═══════════════════════════════════════════════════════════

        public async Task<IActionResult> Noticias(int page = 1)
        {
            const int pageSize = 9;
            var query = _db.Noticias
                .Include(n => n.Admin)
                .OrderByDescending(n => n.FechaPublicacion);

            var total    = await query.CountAsync();
            var noticias = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NoticiaListaVM
                {
                    Id               = n.Id,
                    Titulo           = n.Titulo,
                    Contenido        = n.Contenido,
                    ImagenRuta       = n.ImagenRuta,
                    EnlaceUrl        = n.EnlaceUrl,
                    FechaPublicacion = n.FechaPublicacion,
                    AdminNombre      = n.Admin != null ? n.Admin.NombreUsuario : "",
                    Activo           = n.Activo
                })
                .ToListAsync();

            ViewBag.Page       = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalItems = total;
            return View(noticias);
        }

        public IActionResult CrearNoticia() => View(new CrearNoticiaViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearNoticia(CrearNoticiaViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            string? imagenRuta = null;
            if (vm.Imagen != null && vm.Imagen.Length > 0)
            {
                var ext = Path.GetExtension(vm.Imagen.FileName).ToLowerInvariant();
                var extsPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!extsPermitidas.Contains(ext))
                {
                    ModelState.AddModelError("Imagen", "Solo se permiten imágenes (.jpg, .png, .gif, .webp).");
                    return View(vm);
                }

                if (!EsImagenValida(vm.Imagen))
                {
                    ModelState.AddModelError("Imagen", "El archivo no es una imagen válida.");
                    return View(vm);
                }

                const long maxSize = 5 * 1024 * 1024; // 5 MB
                if (vm.Imagen.Length > maxSize)
                {
                    ModelState.AddModelError("Imagen", "La imagen no puede superar 5 MB.");
                    return View(vm);
                }

                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "noticias");
                Directory.CreateDirectory(uploadsDir);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await vm.Imagen.CopyToAsync(stream);

                imagenRuta = $"/uploads/noticias/{fileName}";
            }

            var adminId = CurrentUserId;

            _db.Noticias.Add(new Noticia
            {
                Titulo           = vm.Titulo.Trim(),
                Contenido        = vm.Contenido.Trim(),
                ImagenRuta       = imagenRuta,
                EnlaceUrl        = SanitizarEnlace(vm.EnlaceUrl),
                FechaPublicacion = DateTime.Now,
                AdminId          = adminId,
                Activo           = true
            });

            await _db.SaveChangesAsync();
            TempData["Exito"] = "Noticia publicada correctamente.";
            return RedirectToAction(nameof(Noticias));
        }

        // GET /Admin/EditarNoticia/{id}
        public async Task<IActionResult> EditarNoticia(int id)
        {
            var n = await _db.Noticias.FindAsync(id);
            if (n == null) return NotFound();

            return View(new EditarNoticiaViewModel
            {
                Id               = n.Id,
                Titulo           = n.Titulo,
                Contenido        = n.Contenido,
                ImagenRutaActual = n.ImagenRuta,
                EnlaceUrl        = n.EnlaceUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarNoticia(EditarNoticiaViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var n = await _db.Noticias.FindAsync(vm.Id);
            if (n == null) return NotFound();

            if (vm.Imagen != null && vm.Imagen.Length > 0)
            {
                var ext = Path.GetExtension(vm.Imagen.FileName).ToLowerInvariant();
                var extsPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!extsPermitidas.Contains(ext))
                {
                    ModelState.AddModelError("Imagen", "Solo se permiten imágenes (.jpg, .png, .gif, .webp).");
                    vm.ImagenRutaActual = n.ImagenRuta;
                    return View(vm);
                }

                if (!EsImagenValida(vm.Imagen))
                {
                    ModelState.AddModelError("Imagen", "El archivo no es una imagen válida.");
                    vm.ImagenRutaActual = n.ImagenRuta;
                    return View(vm);
                }

                const long maxSize = 5 * 1024 * 1024;
                if (vm.Imagen.Length > maxSize)
                {
                    ModelState.AddModelError("Imagen", "La imagen no puede superar 5 MB.");
                    vm.ImagenRutaActual = n.ImagenRuta;
                    return View(vm);
                }

                // Eliminar imagen anterior
                if (!string.IsNullOrEmpty(n.ImagenRuta))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, n.ImagenRuta.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "noticias");
                Directory.CreateDirectory(uploadsDir);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await vm.Imagen.CopyToAsync(stream);

                n.ImagenRuta = $"/uploads/noticias/{fileName}";
            }

            n.Titulo    = vm.Titulo.Trim();
            n.Contenido = vm.Contenido.Trim();
            n.EnlaceUrl = SanitizarEnlace(vm.EnlaceUrl);

            await _db.SaveChangesAsync();
            TempData["Exito"] = "Noticia actualizada correctamente.";
            return RedirectToAction(nameof(Noticias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarNoticia(int id)
        {
            var n = await _db.Noticias.FindAsync(id);
            if (n == null) return NotFound();

            // Eliminar imagen del disco si existe
            if (!string.IsNullOrEmpty(n.ImagenRuta))
            {
                var filePath = Path.Combine(_env.WebRootPath, n.ImagenRuta.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _db.Noticias.Remove(n);
            await _db.SaveChangesAsync();

            TempData["Exito"] = "Noticia eliminada.";
            await RegistrarLog("EliminarNoticia", $"Noticia '{n.Titulo}' eliminada");
            return RedirectToAction(nameof(Noticias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleNoticia(int id)
        {
            var n = await _db.Noticias.FindAsync(id);
            if (n == null) return NotFound();

            n.Activo = !n.Activo;
            await _db.SaveChangesAsync();
            TempData["Exito"] = n.Activo ? $"Noticia '{n.Titulo}' activada." : $"Noticia '{n.Titulo}' desactivada.";
            return RedirectToAction(nameof(Noticias));
        }

        // ═══════════════════════════════════════════════════════════
        //  IMPORTACIÓN MASIVA DE USUARIOS
        // ═══════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CargarUsuarios(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                TempData["Error"] = "Seleccione un archivo Excel válido.";
                return RedirectToAction(nameof(Usuarios));
            }

            const long MaxFileSize = 10 * 1024 * 1024;
            if (archivo.Length > MaxFileSize)
            {
                TempData["Error"] = "El archivo supera el límite de 10 MB.";
                return RedirectToAction(nameof(Usuarios));
            }

            var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls")
            {
                TempData["Error"] = "Solo se permiten archivos Excel (.xlsx o .xls).";
                return RedirectToAction(nameof(Usuarios));
            }

            List<(string Usuario, string Correo, string Contrasena, string? PrimerNombre,
                  string? PrimerApellido, string? Celular, string? Dni)> importados;
            try
            {
                using var stream = archivo.OpenReadStream();
                importados = _excel.ImportarUsuarios(stream);
            }
            catch
            {
                TempData["Error"] = "Error al procesar el archivo. Verifique el formato.";
                return RedirectToAction(nameof(Usuarios));
            }

            if (importados.Count == 0)
            {
                TempData["Error"] = "No se encontraron usuarios válidos en el archivo.";
                return RedirectToAction(nameof(Usuarios));
            }

            int creados = 0, omitidos = 0;
            foreach (var (usuario, correo, clave, nombre, apellido, celular, dni) in importados)
            {
                if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == usuario || u.Correo == correo))
                {
                    omitidos++;
                    continue;
                }

                _db.Usuarios.Add(new Estudiante
                {
                    NombreUsuario   = usuario,
                    Correo          = correo,
                    Contrasena      = BCrypt.Net.BCrypt.HashPassword(clave),
                    Rol             = "Usuario",
                    PrimerNombre    = nombre,
                    PrimerApellido  = apellido,
                    Celular         = celular,
                    Dni             = dni,
                    FechaCreacion   = DateTime.Now,
                    Activo          = true,
                    EsTrial         = true
                });
                creados++;
            }

            await _db.SaveChangesAsync();

            if (omitidos > 0)
                TempData["Advertencia"] = $"{omitidos} usuario(s) omitido(s) por usuario o correo duplicado.";

            if (creados > 0)
            {
                TempData["Exito"] = $"Se importaron {creados} usuario(s) correctamente.";
                await RegistrarLog("ImportarUsuarios", $"Se importaron {creados} usuario(s) desde Excel");
            }
            else
                TempData["Error"] = "No se importó ningún usuario (todos eran duplicados).";

            return RedirectToAction(nameof(Usuarios));
        }

        public IActionResult DescargarPlantillaUsuarios()
        {
            var bytes = _excel.GenerarPlantillaUsuarios();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Plantilla_Usuarios.xlsx");
        }

        // ═══════════════════════════════════════════════════════════
        //  ESTADÍSTICAS POR PREGUNTA
        // ═══════════════════════════════════════════════════════════

        public async Task<IActionResult> EstadisticasPreguntas(int? tipoId)
        {
            var tipos = await _db.TiposExamen.Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();
            ViewBag.Tipos  = tipos;
            ViewBag.TipoId = tipoId;

            var query = _db.PreguntasExamen
                .Include(pe => pe.Pregunta).ThenInclude(p => p!.TipoExamen)
                .Include(pe => pe.AlternativaSeleccionada)
                .AsQueryable();

            if (tipoId.HasValue)
                query = query.Where(pe => pe.Pregunta!.TipoExamenId == tipoId.Value);

            // Carga plana a memoria: evita subconsulta dentro de agregado (SQL Server no lo admite)
            var raw = await query
                .Select(pe => new
                {
                    pe.PreguntaId,
                    Texto      = pe.Pregunta!.TextoPregunta,
                    TipoNom    = pe.Pregunta.TipoExamen != null ? pe.Pregunta.TipoExamen.Nombre : "Sin tipo",
                    EsCorrecta = pe.AlternativaSeleccionadaId != null && pe.AlternativaSeleccionada!.EsCorrecta
                })
                .ToListAsync();

            // Agrupación y cálculo en memoria (LINQ to Objects)
            var stats = raw
                .GroupBy(x => new { x.PreguntaId, x.Texto, x.TipoNom })
                .Select(g => new EstadisticaPreguntaVM
                {
                    PreguntaId    = g.Key.PreguntaId,
                    TextoPregunta = g.Key.Texto,
                    TipoNombre    = g.Key.TipoNom,
                    TotalVeces    = g.Count(),
                    Correctas     = g.Count(x => x.EsCorrecta),
                    Incorrectas   = g.Count(x => !x.EsCorrecta)
                })
                .Where(s => s.TotalVeces >= 1)
                .OrderByDescending(s => (double)s.Incorrectas / s.TotalVeces)
                .Take(100)
                .ToList();

            return View(stats);
        }

        // ═══════════════════════════════════════════════════════════
        //  LOGS DE ACTIVIDAD
        // ═══════════════════════════════════════════════════════════

        public async Task<IActionResult> Logs(int page = 1)
        {
            const int pageSize = 50;
            var query = _db.LogsActividad.Include(l => l.Admin).OrderByDescending(l => l.Fecha);
            var total = await query.CountAsync();
            var logs  = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page       = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalItems = total;
            return View(logs);
        }

        // ── Sugerencias de usuarios ───────────────────────────────
        public async Task<IActionResult> Sugerencias(int page = 1, string filtro = "todas")
        {
            const int pageSize = 10;
            var query = _db.Sugerencias
                .Include(s => s.Usuario)
                .AsQueryable();

            if (filtro == "nuevas")
                query = query.Where(s => !s.Leida);
            else if (filtro == "leidas")
                query = query.Where(s => s.Leida);

            var total     = await query.CountAsync();
            var items     = await query
                .OrderByDescending(s => s.FechaEnvio)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page        = page;
            ViewBag.TotalPages  = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalItems  = total;
            ViewBag.Filtro      = filtro;
            ViewBag.TotalNuevas = await _db.Sugerencias.CountAsync(s => !s.Leida);
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarSugerenciaLeida(int id)
        {
            var s = await _db.Sugerencias.FindAsync(id);
            if (s == null) return NotFound();
            s.Leida = !s.Leida;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Sugerencias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSugerencia(int id)
        {
            var s = await _db.Sugerencias.FindAsync(id);
            if (s == null) return NotFound();
            _db.Sugerencias.Remove(s);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Sugerencia eliminada.";
            return RedirectToAction(nameof(Sugerencias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarTodasLeidas()
        {
            var nuevas = await _db.Sugerencias.Where(s => !s.Leida).ToListAsync();
            nuevas.ForEach(s => s.Leida = true);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"{nuevas.Count} sugerencia(s) marcadas como leídas.";
            return RedirectToAction(nameof(Sugerencias));
        }

        // ── Planes de suscripción (tarjetas modal trial) ──────────
        public async Task<IActionResult> Planes()
        {
            var planes = await _db.PlanesSuscripcion
                .Include(p => p.Caracteristicas)
                .OrderBy(p => p.Orden)
                .ToListAsync();
            return View(planes);
        }

        public IActionResult CrearPlan() => View(new PlanSuscripcion
        {
            EnlaceBoton = "https://wa.me/51936037152",
            TextoBoton  = "¡Suscribirme ya!",
            Activo      = true
        });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPlan(PlanSuscripcion plan)
        {
            ModelState.Remove(nameof(PlanSuscripcion.Caracteristicas));
            if (!ModelState.IsValid) return View(plan);

            plan.ColorPrimario   = ValidarColorHex(plan.ColorPrimario,   "#74c0fc");
            plan.ColorSecundario = ValidarColorHex(plan.ColorSecundario, "#4dabf7");
            plan.FechaCreacion   = DateTime.Now;
            plan.Caracteristicas = ParsearCaracteristicas(Request.Form["CaracteristicasTexto"].ToString());
            _db.PlanesSuscripcion.Add(plan);
            await _db.SaveChangesAsync();
            await RegistrarLog("Crear Plan", $"Plan creado: {plan.Nombre}");
            TempData["Exito"] = $"Plan \"{plan.Nombre}\" creado correctamente.";
            return RedirectToAction(nameof(Planes));
        }

        // Valida que un string sea un color hexadecimal (#RGB o #RRGGBB).
        // Si no lo es, devuelve el valor por defecto. Previene CSS Injection
        // cuando el color se inyecta en atributos style=.
        private static string ValidarColorHex(string? valor, string porDefecto) =>
            !string.IsNullOrWhiteSpace(valor) &&
            System.Text.RegularExpressions.Regex.IsMatch(valor.Trim(), @"^#[0-9A-Fa-f]{3}([0-9A-Fa-f]{3})?$")
                ? valor.Trim()
                : porDefecto;

        public async Task<IActionResult> EditarPlan(int id)
        {
            var plan = await _db.PlanesSuscripcion
                .Include(p => p.Caracteristicas)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (plan == null) return NotFound();
            return View(plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPlan(PlanSuscripcion plan)
        {
            ModelState.Remove(nameof(PlanSuscripcion.Caracteristicas));
            if (!ModelState.IsValid) return View(plan);

            var existing = await _db.PlanesSuscripcion
                .Include(p => p.Caracteristicas)
                .FirstOrDefaultAsync(p => p.Id == plan.Id);
            if (existing == null) return NotFound();

            existing.Nombre          = plan.Nombre;
            existing.Etiqueta        = plan.Etiqueta;
            existing.Precio          = plan.Precio;
            existing.TextoPrecio     = plan.TextoPrecio;
            existing.ColorPrimario   = ValidarColorHex(plan.ColorPrimario,   "#74c0fc");
            existing.ColorSecundario = ValidarColorHex(plan.ColorSecundario, "#4dabf7");
            existing.EsPopular       = plan.EsPopular;
            existing.TextoBadge      = plan.TextoBadge;
            existing.EnlaceBoton     = plan.EnlaceBoton;
            existing.TextoBoton      = plan.TextoBoton;
            existing.Activo          = plan.Activo;
            existing.Orden           = plan.Orden;

            _db.CaracteristicasPlan.RemoveRange(existing.Caracteristicas);
            existing.Caracteristicas = ParsearCaracteristicas(Request.Form["CaracteristicasTexto"].ToString());

            await _db.SaveChangesAsync();
            await RegistrarLog("Editar Plan", $"Plan editado: {plan.Nombre}");
            TempData["Exito"] = $"Plan \"{plan.Nombre}\" actualizado correctamente.";
            return RedirectToAction(nameof(Planes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPlan(int id)
        {
            var plan = await _db.PlanesSuscripcion.FindAsync(id);
            if (plan == null) return NotFound();
            var nombre = plan.Nombre;
            _db.PlanesSuscripcion.Remove(plan);
            await _db.SaveChangesAsync();
            await RegistrarLog("Eliminar Plan", $"Plan eliminado: {nombre}");
            TempData["Exito"] = $"Plan \"{nombre}\" eliminado.";
            return RedirectToAction(nameof(Planes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePlan(int id)
        {
            var plan = await _db.PlanesSuscripcion.FindAsync(id);
            if (plan == null) return NotFound();
            plan.Activo = !plan.Activo;
            await _db.SaveChangesAsync();
            TempData["Exito"] = plan.Activo
                ? $"Plan \"{plan.Nombre}\" activado."
                : $"Plan \"{plan.Nombre}\" desactivado.";
            return RedirectToAction(nameof(Planes));
        }

        // ── Anuncio Global (banner de mantenimiento) ─────────────
        public async Task<IActionResult> AnuncioGlobal()
        {
            var anuncio = await _db.AnunciosGlobales.FirstOrDefaultAsync();
            return View(anuncio);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnuncioGlobal(string mensaje, string tipo, bool activo)
        {
            if (string.IsNullOrWhiteSpace(mensaje))
            {
                TempData["Error"] = "El mensaje no puede estar vacío.";
                return RedirectToAction(nameof(AnuncioGlobal));
            }

            var anuncio = await _db.AnunciosGlobales.FirstOrDefaultAsync();
            var adminId = CurrentUserId;

            if (anuncio == null)
            {
                anuncio = new SimulacroExamen.Models.AnuncioGlobal
                {
                    AdminId = adminId
                };
                _db.AnunciosGlobales.Add(anuncio);
            }

            // Whitelist de tipos permitidos para evitar CSS Class Injection / Stored XSS
            // en el atributo class="alert alert-@Tipo" del layout.
            var tiposValidos = new[] { "info", "warning", "danger", "success", "primary", "secondary" };
            var tipoNormalizado = (tipo ?? "").Trim().ToLowerInvariant();
            if (!tiposValidos.Contains(tipoNormalizado))
                tipoNormalizado = "warning";

            anuncio.Mensaje              = mensaje.Trim();
            anuncio.Tipo                 = tipoNormalizado;
            anuncio.Activo               = activo;
            anuncio.FechaActualizacion   = DateTime.Now;
            anuncio.AdminId              = adminId;

            await _db.SaveChangesAsync();
            await RegistrarLog("Anuncio Global", $"Anuncio {(activo ? "activado" : "desactivado")}: {mensaje.Trim()[..Math.Min(80, mensaje.Trim().Length)]}");

            TempData["Exito"] = activo
                ? "Anuncio activado y visible para todos los usuarios."
                : "Anuncio desactivado.";
            return RedirectToAction(nameof(AnuncioGlobal));
        }

        // ── Helper: parsear características de textarea a colección ──
        private static List<CaracteristicaPlan> ParsearCaracteristicas(string texto) =>
            texto.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                 .Select(l => l.Trim())
                 .Where(l => !string.IsNullOrWhiteSpace(l))
                 .Select((t, i) => new CaracteristicaPlan { Texto = t, Orden = i })
                 .ToList();

        // ── Helper: sanitizar URL (solo permite http/https) ──────────
        private static string? SanitizarEnlace(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var trimmed = url.Trim();
            return trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("http://",  StringComparison.OrdinalIgnoreCase)
                   ? trimmed : null;
        }

        // ── Helper: validar magic bytes de imagen ────────────────────
        private static bool EsImagenValida(IFormFile archivo)
        {
            var header = new byte[12];
            using var stream = archivo.OpenReadStream();
            var read = stream.Read(header, 0, header.Length);
            if (read < 3) return false;

            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (read >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A) return true;
            // GIF87a / GIF89a: 47 49 46 38 37/39 61
            if (read >= 6 &&
                header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
                (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61) return true;
            // WebP: RIFF????WEBP
            if (read >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) return true;

            return false;
        }

        // ═══════════════════════════════════════════════════════════
        //  CONFIGURACIÓN DE CORREO  (solo SuperAdmin)
        // ═══════════════════════════════════════════════════════════

        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ConfiguracionCorreo()
        {
            var cfg = await _db.ConfiguracionCorreo.OrderBy(c => c.Id).FirstOrDefaultAsync();

            var vm = new ConfiguracionCorreoViewModel
            {
                YaConfigurado  = cfg != null,
                Smtp           = cfg?.Smtp           ?? _config["Email:Smtp"]      ?? "smtp.gmail.com",
                Puerto         = cfg?.Puerto         ?? int.Parse(_config["Email:Port"] ?? "587"),
                UsuarioCorreo  = cfg?.UsuarioCorreo  ?? _config["Email:Usuario"]   ?? "",
                NombreRemitente= cfg?.NombreRemitente?? _config["Email:Remitente"] ?? "Simulacro SERUMS",
                UsarSsl        = cfg?.UsarSsl        ?? true,
                // No enviamos la contraseña al cliente por seguridad
                Contrasena     = null
            };

            return View(vm);
        }

        // Whitelist de dominios SMTP permitidos para evitar que un admin
        // apunte el servidor SMTP a endpoints internos (SSRF) o al metadata
        // service de la nube (169.254.169.254).
        private static readonly string[] DominiosSmtpPermitidos = new[]
        {
            "smtp.gmail.com",
            "smtp-mail.outlook.com",
            "smtp.office365.com",
            "smtp.sendgrid.net",
            "smtp.mail.yahoo.com",
            "smtp.zoho.com",
            "email-smtp.us-east-1.amazonaws.com",
            "email-smtp.us-west-2.amazonaws.com"
        };

        [HttpPost, Authorize(Roles = "SuperAdmin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfiguracionCorreo(ConfiguracionCorreoViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var smtpNormalizado = (vm.Smtp ?? "").Trim().ToLowerInvariant();
            if (!DominiosSmtpPermitidos.Contains(smtpNormalizado))
            {
                ModelState.AddModelError(nameof(vm.Smtp),
                    "Servidor SMTP no permitido. Dominios válidos: " + string.Join(", ", DominiosSmtpPermitidos));
                return View(vm);
            }

            // Validar puerto (solo los estándares de SMTP saliente)
            if (vm.Puerto != 25 && vm.Puerto != 465 && vm.Puerto != 587 && vm.Puerto != 2525)
            {
                ModelState.AddModelError(nameof(vm.Puerto),
                    "Puerto SMTP inválido. Valores permitidos: 25, 465, 587, 2525.");
                return View(vm);
            }

            var cfg = await _db.ConfiguracionCorreo.OrderBy(c => c.Id).FirstOrDefaultAsync();

            if (cfg == null)
            {
                cfg = new Models.ConfiguracionCorreo { AdminId = CurrentUserId };
                _db.ConfiguracionCorreo.Add(cfg);
            }

            cfg.Smtp            = smtpNormalizado;
            cfg.Puerto          = vm.Puerto;
            cfg.UsuarioCorreo   = vm.UsuarioCorreo.Trim();
            cfg.NombreRemitente = vm.NombreRemitente.Trim();
            cfg.UsarSsl         = vm.UsarSsl;
            cfg.UltimaActualizacion = DateTime.Now;
            cfg.AdminId         = CurrentUserId;

            // Solo actualiza la contraseña si el admin introdujo una nueva.
            // Se cifra con DataProtection antes de persistir para que un backup
            // comprometido no exponga la contraseña SMTP en texto plano.
            if (!string.IsNullOrWhiteSpace(vm.Contrasena))
                cfg.Contrasena = _secretProtector.Proteger(vm.Contrasena.Trim());

            await _db.SaveChangesAsync();
            await RegistrarLog("ConfiguracionCorreo", "Configuración SMTP actualizada");
            TempData["Exito"] = "Configuración de correo guardada correctamente.";
            return RedirectToAction(nameof(ConfiguracionCorreo));
        }

        [HttpPost, Authorize(Roles = "SuperAdmin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> ProbarCorreo(string correoDestino)
        {
            if (string.IsNullOrWhiteSpace(correoDestino) ||
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(correoDestino))
                return Json(new { ok = false, mensaje = "Correo de destino inválido." });

            try
            {
                await _email.EnviarAsync(
                    correoDestino,
                    "Prueba de configuración SMTP — Simulacro SERUMS",
                    $"""
                    <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;
                                border:1px solid #dee2e6;border-radius:8px;">
                        <h2 style="color:#198754;">✅ Correo de prueba enviado</h2>
                        <p>Si estás leyendo este mensaje, la configuración SMTP está funcionando correctamente.</p>
                        <p style="color:#6c757d;font-size:.85rem;">
                            Enviado el {DateTime.Now:dd/MM/yyyy HH:mm} por el panel de administración.
                        </p>
                    </div>
                    """);

                return Json(new { ok = true, mensaje = $"Correo enviado a {correoDestino}." });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error enviando correo de prueba a {Destino}", correoDestino);
                var detalle = ex.InnerException?.Message ?? ex.Message;
                return Json(new { ok = false, mensaje = $"Error SMTP: {detalle}" });
            }
        }

        // ── Helper: registrar log de actividad ────────────────────
        private async Task RegistrarLog(string accion, string descripcion)
        {
            var adminId = CurrentUserId;
            _db.LogsActividad.Add(new LogActividad
            {
                AdminId     = adminId,
                Accion      = accion,
                Descripcion = descripcion,
                Fecha       = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        // ── GET /Admin/SuscriptoresMes — Solo SuperAdmin ───────────
        /// <summary>
        /// Muestra estudiantes registrados en el mes/año indicado que ya tienen
        /// un plan de suscripción asignado. Solo accesible por SuperAdmin.
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> SuscriptoresMes(int? mes, int? anio)
        {
            var ahora = DateTime.Now;
            int m = (mes  >= 1 && mes  <= 12) ? mes!.Value  : ahora.Month;
            int a = (anio >= 2020 && anio <= ahora.Year) ? anio!.Value : ahora.Year;

            var inicio = new DateTime(a, m, 1);
            var fin    = inicio.AddMonths(1);

            var suscriptores = await _db.Estudiantes
                .Where(e => e.PlanSuscripcionId != null
                         && e.FechaCreacion >= inicio
                         && e.FechaCreacion <  fin)
                .Select(e => new SuscriptorFilaVM
                {
                    Id               = e.Id,
                    NombreCompleto   = (e.PrimerNombre + " " + e.PrimerApellido).Trim(),
                    NombreUsuario    = e.NombreUsuario,
                    Correo           = e.Correo,
                    Celular          = e.Celular ?? "",
                    NombrePlan       = e.PlanSuscripcion!.Nombre,
                    PrecioPlan       = e.PlanSuscripcion.Precio,
                    FechaRegistro    = e.FechaCreacion,
                    FechaVencimiento = e.FechaVencimiento,
                    Activo           = e.Activo,
                })
                .OrderByDescending(s => s.FechaRegistro)
                .ToListAsync();

            // Selector: últimos 12 meses para el desplegable de período
            var meses = Enumerable.Range(0, 12)
                .Select(i => new DateTime(ahora.Year, ahora.Month, 1).AddMonths(-i))
                .Select(d => (d.Month, d.Year,
                    d.ToString("MMMM yyyy",
                        System.Globalization.CultureInfo.GetCultureInfo("es-PE"))))
                .ToList();

            var vm = new SuscriptoresMesViewModel
            {
                Suscriptores     = suscriptores,
                Mes              = m,
                Anio             = a,
                MesesDisponibles = meses,
            };

            return View(vm);
        }
    }
}
