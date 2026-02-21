using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.ViewModels;
using System.Security.Claims;

namespace SimulacroExamen.Controllers
{
    /// <summary>
    /// Controlador exclusivo del rol "Usuario". Gestiona el flujo completo del examen:
    /// inicio → presentación → envío → resultado → historial.
    /// Hereda [Authorize(Roles = "Usuario")] a nivel de clase.
    /// </summary>
    [Authorize(Roles = "Usuario")]
    public class ExamenController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration       _config;

        /// <summary>DbContext e IConfiguration inyectados por DI.</summary>
        public ExamenController(ApplicationDbContext db, IConfiguration config)
        {
            _db     = db;
            _config = config;
        }

        /// <summary>
        /// Obtiene el ID del usuario autenticado desde el claim NameIdentifier.
        /// Este claim se establece en AccountController.Login() al crear la cookie.
        /// Se usa en cada acción para filtrar datos solo del usuario actual.
        /// </summary>
        private int UsuarioId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ── Panel principal del usuario ──────────────────────────────
        /// <summary>
        /// Muestra el panel del usuario con sus estadísticas personales:
        /// - Total de exámenes completados.
        /// - Mejor porcentaje obtenido.
        /// - Resumen del último examen.
        /// - Total de preguntas disponibles en el banco.
        /// Utiliza consultas separadas para cada dato (4 queries, simples y rápidas).
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var uid = UsuarioId; // Guardado en variable local para evitar múltiples accesos al claim

            ViewBag.TotalExamenes = await _db.Examenes
                .CountAsync(e => e.UsuarioId == uid && e.Completado);

            // (double?) permite que MaxAsync devuelva null si no hay exámenes → ?? 0
            ViewBag.MejorPuntaje = await _db.Examenes
                .Where(e => e.UsuarioId == uid && e.Completado && e.TotalPreguntas > 0)
                .Select(e => (double?)((double)e.Puntaje / e.TotalPreguntas * 100))
                .MaxAsync() ?? 0;

            // Proyección anónima: trae solo los campos necesarios para el resumen
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
        /// <summary>
        /// Crea un nuevo examen con preguntas y alternativas en orden aleatorio.
        /// Flujo:
        ///   1. Lee NumeroPreguntas de appsettings.json (default: 20).
        ///   2. Trae todas las preguntas activas con sus alternativas de la BD.
        ///   3. Mezcla las preguntas aleatoriamente y toma hasta NumeroPreguntas.
        ///   4. Crea el registro Examen (Completado=false) y lo guarda.
        ///   5. Para cada pregunta mezcla sus alternativas y guarda el orden
        ///      como string en PreguntaExamen.OrdenAlternativas ("3,1,4,2").
        ///   6. Redirige a Tomar() con el ID del examen recién creado.
        /// Es POST para evitar que un refresh del navegador inicie múltiples exámenes.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarExamen()
        {
            // Número configurable de preguntas por examen; si hay menos en la BD, se usan todas
            int numPreguntas = _config.GetValue<int>("AppSettings:NumeroPreguntas", 20);

            // Carga todas las preguntas con sus alternativas (eager loading con Include)
            var preguntas = await _db.Preguntas
                .Where(p => p.Activo)
                .Include(p => p.Alternativas)
                .ToListAsync();

            if (preguntas.Count == 0)
            {
                TempData["Error"] = "No hay preguntas disponibles en el sistema.";
                return RedirectToAction(nameof(Index));
            }

            // OrderBy(_ => rng.Next()) es una técnica estándar para mezclar listas en LINQ
            var rng       = new Random();
            var mezcladas = preguntas.OrderBy(_ => rng.Next())
                                     .Take(Math.Min(numPreguntas, preguntas.Count))
                                     .ToList();

            // Transacción explícita: si el segundo SaveChanges falla, se revierte todo
            // y no quedan registros Examen huérfanos (sin sus PreguntasExamen).
            await using var tx = await _db.Database.BeginTransactionAsync();

            // Crear el registro de examen; Completado=false hasta que el usuario envíe
            var examen = new Examen
            {
                UsuarioId      = UsuarioId,
                FechaInicio    = DateTime.Now,
                TotalPreguntas = mezcladas.Count,
                Completado     = false
            };

            _db.Examenes.Add(examen);
            await _db.SaveChangesAsync(); // Necesario para obtener examen.Id

            // Crear PreguntaExamen por cada pregunta, guardando el orden aleatorio
            for (int i = 0; i < mezcladas.Count; i++)
            {
                var p = mezcladas[i];

                // Mezclar las alternativas de esta pregunta y guardar sus IDs en orden
                var altsOrden = p.Alternativas.OrderBy(_ => rng.Next())
                                              .Select(a => a.Id)
                                              .ToList();

                _db.PreguntasExamen.Add(new PreguntaExamen
                {
                    ExamenId          = examen.Id,
                    PreguntaId        = p.Id,
                    Orden             = i + 1,                        // 1-based para la vista
                    OrdenAlternativas = string.Join(",", altsOrden)   // Ej: "3,1,4,2"
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync(); // Confirmar ambos SaveChanges como una sola unidad atómica
            return RedirectToAction(nameof(Tomar), new { id = examen.Id });
        }

        // ── GET /Examen/Tomar/{id} ────────────────────────────────────
        /// <summary>
        /// Muestra el formulario del examen con todas las preguntas y sus alternativas.
        /// Solo el propietario del examen puede verlo (filtro UsuarioId == UsuarioId).
        /// Solo muestra exámenes no completados; si ya se completó, redirige al panel.
        /// Reconstruye el orden aleatorio de alternativas desde OrdenAlternativas (string → IDs).
        /// ThenInclude anidado: Examen → PreguntasExamen → Pregunta → Alternativas
        /// </summary>
        public async Task<IActionResult> Tomar(int id)
        {
            // Carga profunda: examen + preguntas + textos + alternativas en una sola consulta
            var examen = await _db.Examenes
                .Include(e => e.PreguntasExamen)
                    .ThenInclude(pe => pe.Pregunta)
                        .ThenInclude(p => p.Alternativas)
                .FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == UsuarioId && !e.Completado);

            if (examen == null)
                return RedirectToAction(nameof(Index)); // Examen no encontrado o ya completado

            var vm = new ExamenViewModel { ExamenId = examen.Id };

            // Construir el ViewModel ordenando según los datos aleatorios persistidos
            foreach (var pe in examen.PreguntasExamen.OrderBy(x => x.Orden))
            {
                // Deserializar el orden de alternativas: "3,1,4,2" → [3, 1, 4, 2]
                // Se usa TryParse en lugar de Parse para evitar FormatException ante datos corruptos
                var idsOrden = pe.OrdenAlternativas
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => int.TryParse(s, out _))
                    .Select(int.Parse)
                    .ToList();

                // Ordenar las alternativas de la pregunta según el orden aleatorio almacenado
                var altsOrdenadas = idsOrden
                    .Select(altId => pe.Pregunta.Alternativas.FirstOrDefault(a => a.Id == altId))
                    .Where(a => a != null) // Filtrar posibles nulos por alternativas eliminadas
                    .Select(a => new AlternativaVM
                    {
                        Id               = a!.Id,
                        TextoAlternativa = a.TextoAlternativa
                    })
                    .ToList();

                vm.Preguntas.Add(new PreguntaExamenVM
                {
                    PreguntaExamenId = pe.Id,          // Se usa como key en el POST del formulario
                    PreguntaId       = pe.PreguntaId,
                    Orden            = pe.Orden,
                    TextoPregunta    = pe.Pregunta.TextoPregunta,
                    Alternativas     = altsOrdenadas
                });
            }

            return View(vm);
        }

        // ── POST /Examen/Enviar ───────────────────────────────────────
        /// <summary>
        /// Procesa las respuestas del formulario, califica el examen y lo marca como completado.
        /// Flujo:
        ///   1. Lee ExamenId del formulario (campo hidden).
        ///   2. Verifica que el examen pertenezca al usuario y no esté completado.
        ///   3. Para cada PreguntaExamen busca la respuesta en el form: "respuesta_{pe.Id}".
        ///   4. Verifica si la alternativa seleccionada es correcta (+1 punto si lo es).
        ///   5. Guarda AlternativaSeleccionadaId, EsCorrecta, Puntaje, FechaFin.
        ///   6. Redirige a Resultado().
        /// IFormCollection se usa para leer claves dinámicas (respuesta_1, respuesta_2, ...).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar(IFormCollection form)
        {
            // Leer el ID del examen desde el campo hidden del formulario
            if (!int.TryParse(form["ExamenId"], out int examenId))
                return RedirectToAction(nameof(Index));

            // Carga con Include para tener las alternativas disponibles para calificar
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
                // Clave del campo radio en el formulario HTML: name="respuesta_{pe.Id}"
                var key = $"respuesta_{pe.Id}";
                if (form.ContainsKey(key) && int.TryParse(form[key], out int altId))
                {
                    var alt = pe.Pregunta.Alternativas.FirstOrDefault(a => a.Id == altId);
                    if (alt != null)
                    {
                        pe.AlternativaSeleccionadaId = altId;
                        pe.EsCorrecta                = alt.EsCorrecta; // true → +1 punto
                        if (alt.EsCorrecta) puntaje++;
                    }
                }
                // Si la clave no existe (pregunta no respondida): EsCorrecta permanece false
            }

            examen.Puntaje    = puntaje;
            examen.FechaFin   = DateTime.Now;
            examen.Completado = true;  // A partir de aquí aparece en el historial

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Resultado), new { id = examenId });
        }

        // ═══════════════════════════════════════════════════════════
        //  RESULTADO
        // ═══════════════════════════════════════════════════════════

        // ── GET /Examen/Resultado/{id} ────────────────────────────────
        /// <summary>
        /// Muestra el resultado detallado de un examen ya completado.
        /// Requiere que el examen pertenezca al usuario y esté marcado como Completado.
        /// Carga dos cadenas de Include() para obtener:
        ///   - Las preguntas en orden (Pregunta → Alternativas) para mostrar la respuesta correcta.
        ///   - La alternativa seleccionada por el usuario (AlternativaSeleccionada).
        /// Si el examen no existe o no está completado, redirige al Historial.
        /// </summary>
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
        /// <summary>
        /// Lista todos los exámenes completados del usuario autenticado, del más reciente al más antiguo.
        /// Solo devuelve exámenes con Completado=true; los incompletos (abandonados) no aparecen.
        /// La vista puede usar el Id de cada examen para navegar a su Resultado().
        /// </summary>
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
