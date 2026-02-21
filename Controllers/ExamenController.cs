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

        // ── Panel principal del usuario ──────────────────────────────
        public async Task<IActionResult> Index()
        {
            var uid = UsuarioId;

            ViewBag.TotalExamenes = await _db.Examenes
                .CountAsync(e => e.UsuarioId == uid && e.Completado);

            ViewBag.MejorPuntaje = await _db.Examenes
                .Where(e => e.UsuarioId == uid && e.Completado && e.TotalPreguntas > 0)
                .Select(e => (double?)((double)e.Puntaje / e.TotalPreguntas * 100))
                .MaxAsync() ?? 0;

            ViewBag.UltimoExamen = await _db.Examenes
                .Where(e => e.UsuarioId == uid && e.Completado)
                .OrderByDescending(e => e.FechaFin)
                .Select(e => new { e.Puntaje, e.TotalPreguntas, e.FechaFin })
                .FirstOrDefaultAsync();

            ViewBag.TotalPreguntas = await _db.Preguntas.CountAsync(p => p.Activo);

            return View();
        }

        // ═══════════════════════════════════════════════════════════
        //  TOMAR EXAMEN
        // ═══════════════════════════════════════════════════════════

        // ── POST /Examen/IniciarExamen ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarExamen()
        {
            int numPreguntas = _config.GetValue<int>("AppSettings:NumeroPreguntas", 20);

            var preguntas = await _db.Preguntas
                .Where(p => p.Activo)
                .Include(p => p.Alternativas)
                .ToListAsync();

            if (preguntas.Count == 0)
            {
                TempData["Error"] = "No hay preguntas disponibles en el sistema.";
                return RedirectToAction(nameof(Index));
            }

            // Mezcla aleatoria de preguntas
            var rng = new Random();
            var mezcladas = preguntas.OrderBy(_ => rng.Next())
                                     .Take(Math.Min(numPreguntas, preguntas.Count))
                                     .ToList();

            // Crear examen
            var examen = new Examen
            {
                UsuarioId      = UsuarioId,
                FechaInicio    = DateTime.Now,
                TotalPreguntas = mezcladas.Count,
                Completado     = false
            };

            _db.Examenes.Add(examen);
            await _db.SaveChangesAsync();

            // Crear PreguntaExamen con orden y alternativas mezcladas
            for (int i = 0; i < mezcladas.Count; i++)
            {
                var p          = mezcladas[i];
                var altsOrden  = p.Alternativas.OrderBy(_ => rng.Next())
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
            return RedirectToAction(nameof(Tomar), new { id = examen.Id });
        }

        // ── GET /Examen/Tomar/{id} ────────────────────────────────────
        public async Task<IActionResult> Tomar(int id)
        {
            var examen = await _db.Examenes
                .Include(e => e.PreguntasExamen)
                    .ThenInclude(pe => pe.Pregunta)
                        .ThenInclude(p => p.Alternativas)
                .FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == UsuarioId && !e.Completado);

            if (examen == null)
                return RedirectToAction(nameof(Index));

            var vm = new ExamenViewModel { ExamenId = examen.Id };

            foreach (var pe in examen.PreguntasExamen.OrderBy(x => x.Orden))
            {
                // Restaurar el orden aleatorio almacenado
                var idsOrden = pe.OrdenAlternativas
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToList();

                var altsOrdenadas = idsOrden
                    .Select(altId => pe.Pregunta.Alternativas
                        .FirstOrDefault(a => a.Id == altId))
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

        // ═══════════════════════════════════════════════════════════
        //  RESULTADO
        // ═══════════════════════════════════════════════════════════

        // ── GET /Examen/Resultado/{id} ────────────────────────────────
        public async Task<IActionResult> Resultado(int id)
        {
            var examen = await _db.Examenes
                .Include(e => e.PreguntasExamen.OrderBy(pe => pe.Orden))
                    .ThenInclude(pe => pe.Pregunta)
                        .ThenInclude(p => p.Alternativas)
                .Include(e => e.PreguntasExamen)
                    .ThenInclude(pe => pe.AlternativaSeleccionada)
                .FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == UsuarioId && e.Completado);

            if (examen == null)
                return RedirectToAction(nameof(Historial));

            var vm = new ResultadoExamenViewModel
            {
                ExamenId       = examen.Id,
                Puntaje        = examen.Puntaje,
                TotalPreguntas = examen.TotalPreguntas,
                Porcentaje     = examen.Porcentaje,
                FechaInicio    = examen.FechaInicio,
                FechaFin       = examen.FechaFin!.Value
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

        // ═══════════════════════════════════════════════════════════
        //  HISTORIAL
        // ═══════════════════════════════════════════════════════════

        // ── GET /Examen/Historial ─────────────────────────────────────
        public async Task<IActionResult> Historial()
        {
            var examenes = await _db.Examenes
                .Where(e => e.UsuarioId == UsuarioId && e.Completado)
                .OrderByDescending(e => e.FechaFin)
                .ToListAsync();

            return View(examenes);
        }
    }
}
