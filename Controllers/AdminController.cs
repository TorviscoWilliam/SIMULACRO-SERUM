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

            return View();
        }

        // ═══════════════════════════════════════════════════════════
        //  USUARIOS
        // ═══════════════════════════════════════════════════════════

        public async Task<IActionResult> Usuarios()
        {
            var usuarios = await _db.Usuarios
                .Where(u => u.Rol == "Usuario")
                .OrderByDescending(u => u.FechaCreacion)
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

        // GET /Admin/Preguntas?tipoId=
        public async Task<IActionResult> Preguntas(int? tipoId)
        {
            var tipos = await _db.TiposExamen.Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync();
            ViewBag.Tipos   = tipos;
            ViewBag.TipoId  = tipoId;

            var query = _db.Preguntas
                .Where(p => p.Activo)
                .Include(p => p.TipoExamen)
                .Include(p => p.Alternativas)
                .AsQueryable();

            if (tipoId.HasValue)
                query = query.Where(p => p.TipoExamenId == tipoId.Value);

            var preguntas = await query.OrderByDescending(p => p.FechaCreacion).ToListAsync();
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
    }
}
