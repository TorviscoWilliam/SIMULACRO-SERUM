using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.ViewModels;
using System.Security.Claims;

namespace SimulacroExamen.Controllers
{
    [Authorize(Roles = "Usuario")]
    public class ExamenController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration       _config;

        public ExamenController(ApplicationDbContext db, IConfiguration config)
        {
            _db     = db;
            _config = config;
        }

        private int UsuarioId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ── Panel principal: muestra los tipos de examen disponibles ──
        public async Task<IActionResult> Index()
        {
            var uid = UsuarioId;

            // Tipos de examen a los que el usuario tiene acceso
            var tiposAcceso = await _db.UsuarioTiposExamen
                .Where(ut => ut.UsuarioId == uid)
                .Include(ut => ut.TipoExamen)
                .Select(ut => ut.TipoExamen)
                .Where(t => t!.Activo)
                .ToListAsync();

            ViewBag.TotalExamenes = await _db.Examenes
                .CountAsync(e => e.UsuarioId == uid && e.Completado);

            ViewBag.MejorPuntaje = await _db.Examenes
                .Where(e => e.UsuarioId == uid && e.Completado && e.TotalPreguntas > 0)
                .Select(e => (double?)((double)e.Puntaje / e.TotalPreguntas * 100))
                .MaxAsync() ?? 0;

            ViewBag.UltimoExamen = await _db.Examenes
                .Where(e => e.UsuarioId == uid && e.Completado)
                .OrderByDescending(e => e.FechaFin)
                .Select(e => new { e.Puntaje, e.TotalPreguntas, e.FechaFin, TipoNombre = e.TipoExamen != null ? e.TipoExamen.Nombre : "" })
                .FirstOrDefaultAsync();

            // Para cada tipo, cuántas preguntas activas tiene
            var preguntasPorTipo = await _db.Preguntas
                .Where(p => p.Activo && p.TipoExamenId != null)
                .GroupBy(p => p.TipoExamenId)
                .Select(g => new { TipoId = g.Key, Cantidad = g.Count() })
                .ToListAsync();

            ViewBag.PreguntasPorTipo = preguntasPorTipo
                .Where(x => x.TipoId.HasValue)
                .ToDictionary(x => x.TipoId!.Value, x => x.Cantidad);
            ViewBag.TiposAcceso      = tiposAcceso;

            var hoy = DateTime.Today;
            var intentosHoy = await _db.Examenes
                .CountAsync(e => e.UsuarioId == uid && e.FechaInicio >= hoy);
            var limiteExtra = await _db.Usuarios
                .Where(u => u.Id == uid)
                .Select(u => u.IntentosExtra)
                .FirstOrDefaultAsync();
            ViewBag.IntentosHoy       = intentosHoy;
            ViewBag.IntentosRestantes = Math.Max(0, 5 + limiteExtra - intentosHoy);
            ViewBag.LimiteDiario      = 5 + limiteExtra;

            return View();
        }

        // ── POST /Examen/IniciarExamen ───────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarExamen(int tipoExamenId, int numPreguntas = 20)
        {
            if (numPreguntas != 20 && numPreguntas != 50 && numPreguntas != 100)
                numPreguntas = 20;

            var uid = UsuarioId;

            // Verificar que el usuario tiene acceso a este tipo
            var tieneAcceso = await _db.UsuarioTiposExamen
                .AnyAsync(ut => ut.UsuarioId == uid && ut.TipoExamenId == tipoExamenId);

            if (!tieneAcceso)
            {
                TempData["Error"] = "No tienes acceso a ese tipo de examen.";
                return RedirectToAction(nameof(Index));
            }

            // Límite de 5 exámenes por día (+ intentos extra asignados por admin)
            var hoy = DateTime.Today;
            var intentosHoy = await _db.Examenes
                .CountAsync(e => e.UsuarioId == uid && e.FechaInicio >= hoy);

            var intentosExtra = await _db.Usuarios
                .Where(u => u.Id == uid)
                .Select(u => u.IntentosExtra)
                .FirstOrDefaultAsync();

            if (intentosHoy >= 5 + intentosExtra)
            {
                TempData["Error"] = $"Has alcanzado el límite de {5 + intentosExtra} exámenes diarios. Vuelve mañana.";
                return RedirectToAction(nameof(Index));
            }

            var preguntas = await _db.Preguntas
                .Where(p => p.Activo && p.TipoExamenId == tipoExamenId)
                .Include(p => p.Alternativas)
                .ToListAsync();

            if (preguntas.Count == 0)
            {
                TempData["Error"] = "No hay preguntas disponibles para ese tipo de examen.";
                return RedirectToAction(nameof(Index));
            }

            // Duración según cantidad de preguntas elegida
            int? duracionSegundos = numPreguntas switch
            {
                50  => 30 * 60,   // 30 minutos
                100 => 60 * 60,   // 60 minutos
                _   => null       // 20 preguntas = sin límite de tiempo
            };

            var rng       = new Random();
            var mezcladas = preguntas.OrderBy(_ => rng.Next())
                                     .Take(Math.Min(numPreguntas, preguntas.Count))
                                     .ToList();

            await using var tx = await _db.Database.BeginTransactionAsync();

            var examen = new Examen
            {
                UsuarioId        = uid,
                TipoExamenId     = tipoExamenId,
                FechaInicio      = DateTime.Now,
                TotalPreguntas   = mezcladas.Count,
                Completado       = false,
                DuracionSegundos = duracionSegundos
            };

            _db.Examenes.Add(examen);
            await _db.SaveChangesAsync();

            for (int i = 0; i < mezcladas.Count; i++)
            {
                var p = mezcladas[i];
                var altsOrden = p.Alternativas.OrderBy(_ => rng.Next())
                                              .Select(a => a.Id)
                                              .ToList();

                _db.PreguntasExamen.Add(new PreguntaExamen
                {
                    ExamenId          = examen.Id,
                    PreguntaId        = p.Id,
                    Orden             = i + 1,
                    OrdenAlternativas = string.Join(",", altsOrden)
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return RedirectToAction(nameof(Tomar), new { id = examen.Id });
        }

        // ── GET /Examen/Tomar/{id} ────────────────────────────────────
        public async Task<IActionResult> Tomar(int id)
        {
            var examen = await _db.Examenes
                .Include(e => e.TipoExamen)
                .Include(e => e.PreguntasExamen)
                    .ThenInclude(pe => pe.Pregunta)
                        .ThenInclude(p => p.Alternativas)
                .FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == UsuarioId && !e.Completado);

            if (examen == null)
                return RedirectToAction(nameof(Index));

            // Calcular segundos restantes según la duración del examen
            if (examen.DuracionSegundos.HasValue)
            {
                var transcurridos = (int)(DateTime.Now - examen.FechaInicio).TotalSeconds;
                var restantes     = Math.Max(0, examen.DuracionSegundos.Value - transcurridos);
                ViewBag.SegundosRestantes = restantes;
            }
            else
            {
                ViewBag.SegundosRestantes = null; // sin límite de tiempo
            }

            var vm = new ExamenViewModel
            {
                ExamenId       = examen.Id,
                TipoExamenNombre = examen.TipoExamen?.Nombre ?? ""
            };

            foreach (var pe in examen.PreguntasExamen.OrderBy(x => x.Orden))
            {
                var idsOrden = pe.OrdenAlternativas
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => int.TryParse(s, out _))
                    .Select(int.Parse)
                    .ToList();

                var altsOrdenadas = idsOrden
                    .Select(altId => pe.Pregunta.Alternativas.FirstOrDefault(a => a.Id == altId))
                    .Where(a => a != null)
                    .Select(a => new AlternativaVM
                    {
                        Id               = a!.Id,
                        TextoAlternativa = a.TextoAlternativa
                    })
                    .ToList();

                vm.Preguntas.Add(new PreguntaExamenVM
                {
                    PreguntaExamenId = pe.Id,
                    PreguntaId       = pe.PreguntaId,
                    Orden            = pe.Orden,
                    TextoPregunta    = pe.Pregunta.TextoPregunta,
                    Alternativas     = altsOrdenadas
                });
            }

            return View(vm);
        }

        // ── POST /Examen/Enviar ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar(IFormCollection form)
        {
            if (!int.TryParse(form["ExamenId"], out int examenId))
                return RedirectToAction(nameof(Index));

            var examen = await _db.Examenes
                .Include(e => e.PreguntasExamen)
                    .ThenInclude(pe => pe.Pregunta)
                        .ThenInclude(p => p.Alternativas)
                .FirstOrDefaultAsync(e => e.Id == examenId && e.UsuarioId == UsuarioId && !e.Completado);

            if (examen == null)
                return RedirectToAction(nameof(Index));

            int puntaje = 0;

            foreach (var pe in examen.PreguntasExamen)
            {
                var key = $"respuesta_{pe.Id}";
                if (form.ContainsKey(key) && int.TryParse(form[key], out int altId))
                {
                    var alt = pe.Pregunta.Alternativas.FirstOrDefault(a => a.Id == altId);
                    if (alt != null)
                    {
                        pe.AlternativaSeleccionadaId = altId;
                        pe.EsCorrecta                = alt.EsCorrecta;
                        if (alt.EsCorrecta) puntaje++;
                    }
                }
            }

            examen.Puntaje    = puntaje;
            examen.FechaFin   = DateTime.Now;
            examen.Completado = true;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Resultado), new { id = examenId });
        }

        // ── GET /Examen/Resultado/{id} ────────────────────────────────
        public async Task<IActionResult> Resultado(int id)
        {
            var examen = await _db.Examenes
                .Include(e => e.TipoExamen)
                .Include(e => e.PreguntasExamen.OrderBy(pe => pe.Orden))
                    .ThenInclude(pe => pe.Pregunta)
                        .ThenInclude(p => p.Alternativas)
                .Include(e => e.PreguntasExamen)
                    .ThenInclude(pe => pe.AlternativaSeleccionada)
                .FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == UsuarioId && e.Completado);

            if (examen == null)
                return RedirectToAction(nameof(Historial));

            var notaPonderada = await _db.Usuarios
                .Where(u => u.Id == UsuarioId)
                .Select(u => u.NotaPonderada)
                .FirstOrDefaultAsync();

            double? notaFinal = notaPonderada.HasValue
                ? Math.Round(examen.PuntajeVigesimal * 0.70 + notaPonderada.Value * 0.30, 2)
                : null;

            var vm = new ResultadoExamenViewModel
            {
                ExamenId         = examen.Id,
                Puntaje          = examen.Puntaje,
                PuntajeVigesimal = examen.PuntajeVigesimal,
                TotalPreguntas   = examen.TotalPreguntas,
                Porcentaje       = examen.Porcentaje,
                FechaInicio      = examen.FechaInicio,
                FechaFin         = examen.FechaFin!.Value,
                TipoExamenNombre = examen.TipoExamen?.Nombre ?? "",
                NotaPonderada    = notaPonderada,
                NotaFinal        = notaFinal
            };

            int num = 1;
            foreach (var pe in examen.PreguntasExamen)
            {
                var correcta = pe.Pregunta.Alternativas.FirstOrDefault(a => a.EsCorrecta);
                vm.Detalles.Add(new DetalleRespuestaVM
                {
                    Numero                = num++,
                    TextoPregunta         = pe.Pregunta.TextoPregunta,
                    RespuestaCorrecta     = correcta?.TextoAlternativa ?? "-",
                    RespuestaSeleccionada = pe.AlternativaSeleccionada?.TextoAlternativa,
                    EsCorrecta            = pe.EsCorrecta
                });
            }

            return View(vm);
        }

        // ── GET /Examen/Configuracion ─────────────────────────────────
        public async Task<IActionResult> Configuracion()
        {
            var usuario = await _db.Usuarios.FindAsync(UsuarioId);
            if (usuario == null) return RedirectToAction(nameof(Index));

            var vm = new ConfiguracionViewModel
            {
                NotaPonderada = usuario.NotaPonderada
            };
            return View(vm);
        }

        // ── POST /Examen/Configuracion ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Configuracion(ConfiguracionViewModel vm)
        {
            var usuario = await _db.Usuarios.FindAsync(UsuarioId);
            if (usuario == null) return RedirectToAction(nameof(Index));

            bool cambioContrasena = !string.IsNullOrWhiteSpace(vm.ContrasenaNueva);

            if (cambioContrasena)
            {
                if (string.IsNullOrWhiteSpace(vm.ContrasenaActual) ||
                    !BCrypt.Net.BCrypt.Verify(vm.ContrasenaActual, usuario.Contrasena))
                {
                    ModelState.AddModelError("ContrasenaActual", "La contraseña actual es incorrecta.");
                    return View(vm);
                }

                if (!ModelState.IsValid)
                    return View(vm);

                usuario.Contrasena = BCrypt.Net.BCrypt.HashPassword(vm.ContrasenaNueva!);
            }

            // Guardar nota ponderada (puede actualizarse independientemente)
            usuario.NotaPonderada = vm.NotaPonderada;

            await _db.SaveChangesAsync();
            TempData["Exito"] = cambioContrasena
                ? "Contraseña y nota ponderada actualizadas correctamente."
                : "Nota ponderada actualizada correctamente.";

            return RedirectToAction(nameof(Configuracion));
        }

        // ── GET /Examen/Noticias ──────────────────────────────────────
        public async Task<IActionResult> Noticias()
        {
            var noticias = await _db.Noticias
                .Include(n => n.Admin)
                .Where(n => n.Activo)
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

        // ── GET /Examen/Historial ─────────────────────────────────────
        public async Task<IActionResult> Historial()
        {
            var examenes = await _db.Examenes
                .Include(e => e.TipoExamen)
                .Where(e => e.UsuarioId == UsuarioId && e.Completado)
                .OrderByDescending(e => e.FechaFin)
                .ToListAsync();

            return View(examenes);
        }
    }
}
