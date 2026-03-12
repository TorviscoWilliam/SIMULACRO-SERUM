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

        public AdminController(ApplicationDbContext db, IExcelService excel)
        {
            _db    = db;
            _excel = excel;
        }

        // ── Dashboard ────────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsuarios  = await _db.Usuarios.CountAsync(u => u.Rol == "Usuario" && u.Activo);
            ViewBag.TotalPreguntas = await _db.Preguntas.CountAsync(p => p.Activo);
            ViewBag.TotalExamenes  = await _db.Examenes.CountAsync(e => e.Completado);
            ViewBag.PromedioGlobal = await _db.Examenes
                .Where(e => e.Completado && e.TotalPreguntas > 0)
                .AverageAsync(e => (double?)((double)e.Puntaje / e.TotalPreguntas * 100)) ?? 0;

            // ── Datos para gráficos ──────────────────────────────────
            var hoy   = DateTime.Today;
            var hace7 = hoy.AddDays(-6);

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

            var pregsPorTipo = await _db.Preguntas
                .Where(p => p.Activo && p.TipoExamenId != null)
                .GroupBy(p => p.TipoExamen!.Nombre)
                .Select(g => new { Tipo = g.Key, Cantidad = g.Count() })
                .OrderByDescending(x => x.Cantidad)
                .Take(10)
                .ToListAsync();

            ViewBag.TipoLabels = pregsPorTipo.Select(x => x.Tipo).ToArray();
            ViewBag.TipoCounts = pregsPorTipo.Select(x => x.Cantidad).ToArray();

            return View();
        }

        // ═══════════════════════════════════════════════════════════
        //  USUARIOS
        // ═══════════════════════════════════════════════════════════

        public async Task<IActionResult> Usuarios(int page = 1)
        {
            const int pageSize = 15;

            var query = _db.Usuarios.Where(u => u.Rol == "Usuario");
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
                    TiposAsignados = u.UsuariosTipoExamen.Select(ut => ut.TipoExamen.Nombre).ToList()
                })
                .ToListAsync();

            ViewBag.Page       = page;
            ViewBag.PageSize   = pageSize;
            ViewBag.TotalItems = total;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View(usuarios);
        }

        public IActionResult CrearUsuario() => View(new CrearUsuarioViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearUsuario(CrearUsuarioViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == vm.NombreUsuario))
            {
                ModelState.AddModelError(nameof(vm.NombreUsuario), "El nombre de usuario ya existe");
                return View(vm);
            }

            if (await _db.Usuarios.AnyAsync(u => u.Correo == vm.Correo))
            {
                ModelState.AddModelError(nameof(vm.Correo), "El correo ya está registrado");
                return View(vm);
            }

            _db.Usuarios.Add(new Usuario
            {
                NombreUsuario = vm.NombreUsuario,
                Correo        = vm.Correo,
                Contrasena    = BCrypt.Net.BCrypt.HashPassword(vm.Contrasena),
                Rol           = "Usuario",
                FechaCreacion = DateTime.Now,
                Activo        = true
            });

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Usuario '{vm.NombreUsuario}' creado correctamente.";
            return RedirectToAction(nameof(Usuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUsuario(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null || u.Rol == "Admin") return NotFound();

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
                .Where(u => u.Rol == "Usuario")
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

        // GET /Admin/Preguntas?tipoId=&page=
        public async Task<IActionResult> Preguntas(int? tipoId, int page = 1)
        {
            const int pageSize = 20;

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

            int guardadas = 0;
            foreach (var vm in importadas)
            {
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
            TempData["Exito"] = $"Se importaron {guardadas} pregunta(s) correctamente.";
            return RedirectToAction(nameof(Preguntas));
        }

        public IActionResult DescargarPlantilla()
        {
            var bytes = _excel.GenerarPlantillaPreguntas();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Plantilla_Preguntas.xlsx");
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
    }
}
