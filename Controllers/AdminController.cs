using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.Services;
using SimulacroExamen.ViewModels;

namespace SimulacroExamen.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IExcelService        _excel;
        private readonly IWebHostEnvironment  _env;

        public AdminController(ApplicationDbContext db, IExcelService excel, IWebHostEnvironment env)
        {
            _db    = db;
            _excel = excel;
            _env   = env;
        }

        // ── Dashboard ────────────────────────────────────────────────
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
            ViewBag.TotalUsuarios  = await _db.Usuarios.CountAsync(u => u.Rol == "Usuario" && u.Activo);
            ViewBag.TotalAdmins    = await _db.Usuarios.CountAsync(u => u.Rol == "Admin"   && u.Activo);
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

        public async Task<IActionResult> Usuarios(int page = 1)
        {
            const int pageSize = 15;

            var query = _db.Usuarios.AsQueryable();
            var total = await query.CountAsync();

            var usuarios = await query
                .OrderByDescending(u => u.FechaCreacion)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UsuarioListaViewModel
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
                    TiposAsignados = u.UsuariosTipoExamen.Select(ut => ut.TipoExamen.Nombre).ToList(),
                    IntentosExtra  = u.IntentosExtra,
                    NombreCompleto = (u.PrimerNombre != null || u.PrimerApellido != null)
                        ? ((u.PrimerNombre ?? "") +
                           (u.SegundoNombre  != null ? " " + u.SegundoNombre  : "") +
                           (u.PrimerApellido != null ? " " + u.PrimerApellido : "") +
                           (u.SegundoApellido != null ? " " + u.SegundoApellido : "")).Trim()
                        : null,
                    Celular = u.Celular,
                    Dni     = u.Dni
                })
                .ToListAsync();

            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalItems = total;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View(usuarios);
        }

        // POST /Admin/AjustarIntentos
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjustarIntentos(int id, int intentosExtra)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();

            u.IntentosExtra = Math.Max(0, intentosExtra);
            await _db.SaveChangesAsync();

            TempData["Exito"] = $"Intentos diarios de '{u.NombreUsuario}' actualizados a {5 + u.IntentosExtra}.";
            return RedirectToAction(nameof(Usuarios));
        }

        public IActionResult CrearUsuario() => View(new CrearUsuarioViewModel());

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

            var rolValido = vm.Rol == "Admin" || vm.Rol == "Usuario";
            if (!rolValido)
            {
                ModelState.AddModelError(nameof(vm.Rol), "Rol inválido");
                return View(vm);
            }

            _db.Usuarios.Add(new Usuario
            {
                NombreUsuario = nombreUpper,
                Correo        = vm.Correo.Trim(),
                Contrasena    = BCrypt.Net.BCrypt.HashPassword(vm.Contrasena),
                Rol           = vm.Rol,
                FechaCreacion = DateTime.Now,
                Activo        = true
            });

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Usuario '{nombreUpper}' creado correctamente.";
            return RedirectToAction(nameof(Usuarios));
        }

        // GET /Admin/EditarUsuario/{id}
        public async Task<IActionResult> EditarUsuario(int id)
        {
            var usuario = await _db.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            var vm = new EditarUsuarioViewModel
            {
                Id             = usuario.Id,
                NombreUsuario  = usuario.NombreUsuario,
                Correo         = usuario.Correo,
                Rol            = usuario.Rol,
                PrimerNombre   = usuario.PrimerNombre,
                SegundoNombre  = usuario.SegundoNombre,
                PrimerApellido = usuario.PrimerApellido,
                SegundoApellido = usuario.SegundoApellido,
                Celular        = usuario.Celular,
                Dni            = usuario.Dni
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarUsuario(EditarUsuarioViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

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

            if (vm.Rol != "Admin" && vm.Rol != "Usuario")
            {
                ModelState.AddModelError(nameof(vm.Rol), "Rol inválido");
                return View(vm);
            }

            usuario.NombreUsuario   = nombreUpper;
            usuario.Correo          = vm.Correo.Trim();
            usuario.Rol             = vm.Rol;
            usuario.PrimerNombre    = string.IsNullOrWhiteSpace(vm.PrimerNombre)    ? null : vm.PrimerNombre.Trim().ToUpperInvariant();
            usuario.SegundoNombre   = string.IsNullOrWhiteSpace(vm.SegundoNombre)   ? null : vm.SegundoNombre.Trim().ToUpperInvariant();
            usuario.PrimerApellido  = string.IsNullOrWhiteSpace(vm.PrimerApellido)  ? null : vm.PrimerApellido.Trim().ToUpperInvariant();
            usuario.SegundoApellido = string.IsNullOrWhiteSpace(vm.SegundoApellido) ? null : vm.SegundoApellido.Trim().ToUpperInvariant();
            usuario.Celular         = string.IsNullOrWhiteSpace(vm.Celular) ? null : vm.Celular.Trim();
            usuario.Dni             = string.IsNullOrWhiteSpace(vm.Dni)    ? null : vm.Dni.Trim();

            if (!string.IsNullOrWhiteSpace(vm.ContrasenaNueva))
                usuario.Contrasena = BCrypt.Net.BCrypt.HashPassword(vm.ContrasenaNueva);

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Usuario '{nombreUpper}' actualizado correctamente.";
            return RedirectToAction(nameof(Usuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUsuario(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();

            // No permitir desactivarse a uno mismo
            var currentId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            if (u.Id == currentId)
            {
                TempData["Error"] = "No puedes desactivar tu propia cuenta.";
                return RedirectToAction(nameof(Usuarios));
            }

            u.Activo = !u.Activo;
            await _db.SaveChangesAsync();

            TempData["Exito"] = u.Activo
                ? $"Usuario '{u.NombreUsuario}' activado."
                : $"Usuario '{u.NombreUsuario}' desactivado.";

            return RedirectToAction(nameof(Usuarios));
        }

        public async Task<IActionResult> ExportarUsuarios()
        {
            var usuarios = await _db.Usuarios
                .OrderBy(u => u.NombreUsuario)
                .Select(u => new UsuarioListaViewModel
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
                    TiposAsignados = u.UsuariosTipoExamen.Select(ut => ut.TipoExamen.Nombre).ToList()
                })
                .ToListAsync();

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
            var usuario = await _db.Usuarios
                .Include(u => u.UsuariosTipoExamen)
                .FirstOrDefaultAsync(u => u.Id == id && u.Rol == "Usuario");

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
            var usuario = await _db.Usuarios
                .Include(u => u.UsuariosTipoExamen)
                .FirstOrDefaultAsync(u => u.Id == usuarioId && u.Rol == "Usuario");

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
            var tipos = await _db.TiposExamen.OrderBy(t => t.Nombre).ToListAsync();
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
            return View(tipo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarTipo(TipoExamen tipo)
        {
            if (!ModelState.IsValid) return View(tipo);

            if (await _db.TiposExamen.AnyAsync(t => t.Nombre == tipo.Nombre && t.Id != tipo.Id))
            {
                ModelState.AddModelError(nameof(tipo.Nombre), "Ya existe un tipo con ese nombre.");
                return View(tipo);
            }

            var existing = await _db.TiposExamen.FindAsync(tipo.Id);
            if (existing == null) return NotFound();

            existing.Nombre          = tipo.Nombre;
            existing.NumeroPreguntas = tipo.NumeroPreguntas;
            await _db.SaveChangesAsync();

            TempData["Exito"] = $"Tipo '{existing.Nombre}' actualizado correctamente.";
            return RedirectToAction(nameof(TiposExamen));
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

        public async Task<IActionResult> Noticias()
        {
            var noticias = await _db.Noticias
                .Include(n => n.Admin)
                .OrderByDescending(n => n.FechaPublicacion)
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

            var adminId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            _db.Noticias.Add(new Noticia
            {
                Titulo           = vm.Titulo.Trim(),
                Contenido        = vm.Contenido.Trim(),
                ImagenRuta       = imagenRuta,
                EnlaceUrl        = string.IsNullOrWhiteSpace(vm.EnlaceUrl) ? null : vm.EnlaceUrl.Trim(),
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
            n.EnlaceUrl = string.IsNullOrWhiteSpace(vm.EnlaceUrl) ? null : vm.EnlaceUrl.Trim();

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
            return RedirectToAction(nameof(Noticias));
        }
    }
}
