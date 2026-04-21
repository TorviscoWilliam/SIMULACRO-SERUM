using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using SimulacroExamen.Models;
using SimulacroExamen.ViewModels;

namespace SimulacroExamen.Controllers
{
    [Authorize(Roles = "Usuario")]
    public class ExamenController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration       _config;

        public ExamenController(ApplicationDbContext db, IConfiguration config)
        {
            _db     = db;
            _config = config;
        }

        // ── Panel principal: muestra los tipos de examen disponibles ──
        public async Task<IActionResult> Index()
        {
            var uid = CurrentUserId;

            // Tipos de examen a los que el usuario tiene acceso
            var tipoIds = await _db.UsuarioTiposExamen
                .Where(ut => ut.UsuarioId == uid)
                .Select(ut => ut.TipoExamenId)
                .ToListAsync();

            List<TipoExamen> tiposAcceso;
            try
            {
                tiposAcceso = await _db.TiposExamen
                    .Where(t => tipoIds.Contains(t.Id) && t.Activo)
                    .ToListAsync();
            }
            catch
            {
                // NumeroPreguntas u otra columna nueva no existe aún en la BD de producción
                tiposAcceso = new List<TipoExamen>();
            }

            // Cargar las opciones de duración por separado para que funcione
            // aunque la tabla aún no exista en Azure.
            try
            {
                var idList  = tiposAcceso.Select(t => t.Id).ToList();
                var opciones = await _db.OpcionesDuracion
                    .Where(o => idList.Contains(o.TipoExamenId))
                    .OrderBy(o => o.Orden)
                    .ToListAsync();
                foreach (var t in tiposAcceso)
                    t.OpcionesDuracion = opciones.Where(o => o.TipoExamenId == t.Id).ToList();
            }
            catch
            {
                // Tabla OpcionesDuracion aún no existe en esta instancia de Azure.
            }

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

            int limiteExtra = 0;
            try
            {
                limiteExtra = await _db.Estudiantes
                    .Where(u => u.Id == uid)
                    .Select(u => u.IntentosExtra)
                    .FirstOrDefaultAsync();
            }
            catch { /* columna IntentosExtra aún no existe en esta BD */ }

            ViewBag.IntentosHoy       = intentosHoy;
            ViewBag.IntentosRestantes = Math.Max(0, 5 + limiteExtra - intentosHoy);
            ViewBag.LimiteDiario      = 5 + limiteExtra;

            // Estado trial y suscripción
            bool esTrial   = false;
            DateTime? fechaVenc = null;
            try
            {
                var usuarioData = await _db.Estudiantes
                    .Where(u => u.Id == uid)
                    .Select(u => new { u.EsTrial, u.FechaVencimiento })
                    .FirstOrDefaultAsync();
                esTrial   = usuarioData?.EsTrial ?? false;
                fechaVenc = usuarioData?.FechaVencimiento;
            }
            catch { /* columnas EsTrial/FechaVencimiento aún no existen en esta BD */ }

            var examenesCompletados = await _db.Examenes
                .CountAsync(e => e.UsuarioId == uid && e.Completado);
            bool vencida    = !esTrial && fechaVenc.HasValue && fechaVenc.Value < DateTime.Now;
            int  diasRestantes = (!esTrial && fechaVenc.HasValue && fechaVenc.Value >= DateTime.Now)
                                 ? (int)Math.Ceiling((fechaVenc.Value - DateTime.Now).TotalDays)
                                 : 0;

            ViewBag.EsTrial             = esTrial;
            ViewBag.ExamenesCompletados = examenesCompletados;
            ViewBag.TrialAgotado        = esTrial && examenesCompletados >= 1;
            ViewBag.SuscripcionVencida  = vencida;
            ViewBag.FechaVencimiento    = fechaVenc;
            ViewBag.DiasRestantes       = diasRestantes;
            ViewBag.ProximoAVencer      = !esTrial && !vencida && diasRestantes > 0 && diasRestantes <= 7;

            // Últimos 10 exámenes para gráfica personal
            var ultimos10 = await _db.Examenes
                .Where(e => e.UsuarioId == uid && e.Completado && e.TotalPreguntas > 0 && e.FechaFin.HasValue)
                .OrderByDescending(e => e.FechaFin)
                .Take(10)
                .Select(e => new
                {
                    Fecha      = e.FechaFin!.Value,
                    Porcentaje = Math.Round((double)e.Puntaje / e.TotalPreguntas * 100, 1),
                    TipoNombre = e.TipoExamen != null ? e.TipoExamen.Nombre : "General"
                })
                .ToListAsync();

            // Reverso para que el más antiguo quede primero en la gráfica
            ultimos10.Reverse();
            ViewBag.GraficaFechas     = ultimos10.Select(e => e.Fecha.ToString("dd/MM")).ToList();
            ViewBag.GraficaPorcentajes = ultimos10.Select(e => e.Porcentaje).ToList();
            ViewBag.GraficaTipos      = ultimos10.Select(e => e.TipoNombre).ToList();

            // Planes de suscripción para el modal trial
            try
            {
                ViewBag.PlanesSuscripcion = await _db.PlanesSuscripcion
                    .Include(p => p.Caracteristicas)
                    .Where(p => p.Activo)
                    .OrderBy(p => p.Orden)
                    .ToListAsync();
            }
            catch { ViewBag.PlanesSuscripcion = new List<PlanSuscripcion>(); }

            // Examen incompleto (reanudable) — DuracionSegundos puede no existir en BD vieja
            try
            {
                ViewBag.ExamenEnCurso = await _db.Examenes
                    .Include(e => e.TipoExamen)
                    .Where(e => e.UsuarioId == uid && !e.Completado)
                    .OrderByDescending(e => e.FechaInicio)
                    .Select(e => new {
                        e.Id,
                        e.FechaInicio,
                        e.TotalPreguntas,
                        e.DuracionSegundos,
                        TipoNombre = e.TipoExamen != null ? e.TipoExamen.Nombre : "General",
                        RespuestasDadas = e.PreguntasExamen.Count(pe => pe.AlternativaSeleccionadaId != null)
                    })
                    .FirstOrDefaultAsync();
            }
            catch { ViewBag.ExamenEnCurso = null; }

            return View();
        }

        // ── POST /Examen/IniciarExamen ───────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarExamen(int tipoExamenId,
                                                        int duracionMinutosPersonalizada = 0,
                                                        int numeroPreguntasOpcion = 0)
        {
            var uid = CurrentUserId;

            // Verificar que el usuario tiene acceso a este tipo
            var tieneAcceso = await _db.UsuarioTiposExamen
                .AnyAsync(ut => ut.UsuarioId == uid && ut.TipoExamenId == tipoExamenId);

            if (!tieneAcceso)
            {
                TempData["Error"] = "No tienes acceso a ese tipo de examen.";
                return RedirectToAction(nameof(Index));
            }

            // Si ya existe un examen incompleto, reanudar ese
            var examenExistente = await _db.Examenes
                .FirstOrDefaultAsync(e => e.UsuarioId == uid && !e.Completado);

            if (examenExistente != null)
                return RedirectToAction(nameof(Tomar), new { id = examenExistente.Id });

            // ── Restricciones de modo trial y suscripción vencida ───
            bool esTrialUser     = false;
            int  intentosExtra   = 0;
            DateTime? fechaVencUser = null;
            try
            {
                var data = await _db.Estudiantes
                    .Where(u => u.Id == uid)
                    .Select(u => new { u.EsTrial, u.IntentosExtra, u.FechaVencimiento })
                    .FirstOrDefaultAsync();
                if (data != null)
                {
                    esTrialUser   = data.EsTrial;
                    intentosExtra = data.IntentosExtra;
                    fechaVencUser = data.FechaVencimiento;
                }
            }
            catch { /* columnas aún no existen en esta BD; se continúa sin restricciones */ }

            // Bloquear si la suscripción está vencida
            if (!esTrialUser && fechaVencUser.HasValue && fechaVencUser.Value < DateTime.Now)
            {
                TempData["Error"] = "Tu suscripción ha vencido. Renuévala para continuar practicando.";
                return RedirectToAction(nameof(Index));
            }

            if (esTrialUser)
            {
                var examenesTotal = await _db.Examenes
                    .CountAsync(e => e.UsuarioId == uid && e.Completado);

                if (examenesTotal >= 1)
                {
                    TempData["Error"] = "Has agotado tu examen de prueba. Contacta al administrador para obtener acceso completo.";
                    return RedirectToAction(nameof(Index));
                }

                // En modo trial siempre 20 preguntas máximo
                duracionMinutosPersonalizada = 0; // forzar sin duración personalizada en trial
            }

            // Límite de 5 exámenes por día (+ intentos extra asignados por admin)
            var hoy = DateTime.Today;
            var intentosHoy = await _db.Examenes
                .CountAsync(e => e.UsuarioId == uid && e.FechaInicio >= hoy);

            if (intentosHoy >= 5 + intentosExtra)
            {
                TempData["Error"] = $"Has alcanzado el límite de {5 + intentosExtra} exámenes diarios. Vuelve mañana.";
                return RedirectToAction(nameof(Index));
            }

            // Tipo de examen: NumeroPreguntas viene de la configuración del admin, no del form.
            var tipo = await _db.TiposExamen.FindAsync(tipoExamenId);
            if (tipo == null)
            {
                TempData["Error"] = "Tipo de examen no encontrado.";
                return RedirectToAction(nameof(Index));
            }

            // numPreguntas: usa la opción seleccionada si viene > 0,
            // si no usa el valor configurado en el tipo, y en trial máx 20.
            int numPreguntas = numeroPreguntasOpcion > 0 ? numeroPreguntasOpcion : tipo.NumeroPreguntas;
            if (esTrialUser)
                numPreguntas = Math.Min(numPreguntas, 20);

            var preguntas = await _db.Preguntas
                .Where(p => p.Activo && p.TipoExamenId == tipoExamenId)
                .Include(p => p.Alternativas)
                .ToListAsync();

            if (preguntas.Count == 0)
            {
                TempData["Error"] = "No hay preguntas disponibles para ese tipo de examen.";
                return RedirectToAction(nameof(Index));
            }

            // Duración: 1) personalizada elegida por el estudiante,
            // 2) fija del TipoExamen, 3) sin límite.
            int? duracionSegundos;
            if (duracionMinutosPersonalizada > 0)
                duracionSegundos = duracionMinutosPersonalizada * 60;
            else if (tipo.DuracionMinutos.HasValue)
                duracionSegundos = tipo.DuracionMinutos * 60;
            else
                duracionSegundos = null;

            var rng       = new Random();
            var mezcladas = preguntas.OrderBy(_ => rng.Next())
                                     .Take(Math.Min(numPreguntas, preguntas.Count))
                                     .ToList();

            // Paso 1: guardar Examen + PreguntasExamen en UNA transacción.
            // Si algo falla aquí, hacemos rollback y no se crea nada.
            var preguntasConOrden = mezcladas.Select((p, i) => new
            {
                Pregunta  = p,
                Orden     = i + 1,
                AltsOrden = p.Alternativas.OrderBy(_ => rng.Next()).Select(a => a.Id).ToList()
            }).ToList();

            var examen = new Examen
            {
                UsuarioId        = uid,
                TipoExamenId     = tipoExamenId,
                FechaInicio      = DateTime.Now,
                TotalPreguntas   = mezcladas.Count,
                Completado       = false,
                DuracionSegundos = duracionSegundos
            };

            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                _db.Examenes.Add(examen);
                await _db.SaveChangesAsync();

                foreach (var item in preguntasConOrden)
                {
                    _db.PreguntasExamen.Add(new PreguntaExamen
                    {
                        ExamenId   = examen.Id,
                        PreguntaId = item.Pregunta.Id,
                        Orden      = item.Orden
                    });
                }
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }

            // Paso 2: intentar persistir OrdenesAlternativaExamen en una operación
            // INDEPENDIENTE, fuera de la transacción anterior. Si la tabla no existe
            // en el esquema desplegado (Azure con versión previa), SaveChangesAsync
            // falla SIN abortar el examen ya confirmado. Limpiamos el tracker para
            // que el DbContext no quede en estado inconsistente.
            try
            {
                var peIds = await _db.PreguntasExamen
                    .Where(pe => pe.ExamenId == examen.Id)
                    .OrderBy(pe => pe.Orden)
                    .Select(pe => pe.Id)
                    .ToListAsync();

                for (int i = 0; i < preguntasConOrden.Count; i++)
                {
                    var altIds = preguntasConOrden[i].AltsOrden;
                    for (int j = 0; j < altIds.Count; j++)
                    {
                        _db.OrdenesAlternativaExamen.Add(new OrdenAlternativaExamen
                        {
                            PreguntaExamenId = peIds[i],
                            AlternativaId    = altIds[j],
                            Orden            = j
                        });
                    }
                }
                await _db.SaveChangesAsync();
            }
            catch
            {
                // Tabla OrdenesAlternativaExamen aún no existe: limpiar los
                // OrdenAlternativaExamen que quedaron trackeados en memoria
                // para que posteriores SaveChangesAsync del mismo contexto no fallen.
                var pendientes = _db.ChangeTracker.Entries<OrdenAlternativaExamen>()
                    .Where(e => e.State == EntityState.Added)
                    .ToList();
                foreach (var entry in pendientes)
                    entry.State = EntityState.Detached;
            }

            return RedirectToAction(nameof(Tomar), new { id = examen.Id });
        }

        // ── POST /Examen/GuardarRespuesta (AJAX) ─────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarRespuesta(int preguntaExamenId, int? alternativaId)
        {
            var pe = await _db.PreguntasExamen
                .Include(p => p.Examen)
                .Include(p => p.Pregunta).ThenInclude(p => p.Alternativas)
                .FirstOrDefaultAsync(p => p.Id == preguntaExamenId
                                       && p.Examen.UsuarioId == CurrentUserId
                                       && !p.Examen.Completado);

            if (pe == null) return NotFound();

            if (alternativaId.HasValue)
            {
                var alt = pe.Pregunta.Alternativas.FirstOrDefault(a => a.Id == alternativaId.Value);
                if (alt != null)
                {
                    pe.AlternativaSeleccionadaId = alt.Id;
                }
            }
            else
            {
                pe.AlternativaSeleccionadaId = null;
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        // ── POST /Examen/AbandonarExamen ──────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AbandonarExamen(int id)
        {
            var examen = await _db.Examenes
                .Include(e => e.PreguntasExamen)
                .FirstOrDefaultAsync(e => e.Id == id
                                       && e.UsuarioId == CurrentUserId
                                       && !e.Completado);

            if (examen != null)
            {
                _db.PreguntasExamen.RemoveRange(examen.PreguntasExamen);
                _db.Examenes.Remove(examen);
                await _db.SaveChangesAsync();
            }

            TempData["Exito"] = "Examen anterior descartado. Puedes iniciar uno nuevo.";
            return RedirectToAction(nameof(Index));
        }

        // ── GET /Examen/Tomar/{id} ────────────────────────────────────
        public async Task<IActionResult> Tomar(int id)
        {
            // NO incluir OrdenesAlternativaExamen aquí: si la tabla no existe en Azure
            // el query completo falla. Se carga por separado en try/catch abajo.
            var examen = await _db.Examenes
                .Include(e => e.TipoExamen)
                .Include(e => e.PreguntasExamen)
                    .ThenInclude(pe => pe.Pregunta)
                        .ThenInclude(p => p.Alternativas)
                .FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == CurrentUserId && !e.Completado);

            if (examen == null)
                return RedirectToAction(nameof(Index));

            // Cargar el orden de alternativas en un bloque independiente
            // para que funcione aunque la tabla aún no exista en la BD.
            var ordenAlt = new Dictionary<int, List<int>>();
            try
            {
                var peIds = examen.PreguntasExamen.Select(pe => pe.Id).ToList();
                var ordenes = await _db.OrdenesAlternativaExamen
                    .Where(o => peIds.Contains(o.PreguntaExamenId))
                    .ToListAsync();

                ordenAlt = ordenes
                    .GroupBy(o => o.PreguntaExamenId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(o => o.Orden).Select(o => o.AlternativaId).ToList());
            }
            catch
            {
                // Tabla OrdenesAlternativaExamen aún no existe; se usará el orden original.
            }

            if (examen.DuracionSegundos.HasValue)
            {
                var transcurridos = (int)(DateTime.Now - examen.FechaInicio).TotalSeconds;
                ViewBag.SegundosRestantes = Math.Max(0, examen.DuracionSegundos.Value - transcurridos);
            }
            else
            {
                ViewBag.SegundosRestantes = null;
            }

            var vm = new ExamenViewModel
            {
                ExamenId         = examen.Id,
                TipoExamenNombre = examen.TipoExamen?.Nombre ?? ""
            };

            foreach (var pe in examen.PreguntasExamen.OrderBy(x => x.Orden))
            {
                List<AlternativaVM> altsOrdenadas;

                if (ordenAlt.TryGetValue(pe.Id, out var altIds) && altIds.Any())
                {
                    altsOrdenadas = altIds
                        .Select(altId => pe.Pregunta.Alternativas.FirstOrDefault(a => a.Id == altId))
                        .Where(a => a != null)
                        .Select(a => new AlternativaVM { Id = a!.Id, TextoAlternativa = a.TextoAlternativa })
                        .ToList();
                }
                else
                {
                    altsOrdenadas = pe.Pregunta.Alternativas
                        .Select(a => new AlternativaVM { Id = a.Id, TextoAlternativa = a.TextoAlternativa })
                        .ToList();
                }

                vm.Preguntas.Add(new PreguntaExamenVM
                {
                    PreguntaExamenId          = pe.Id,
                    PreguntaId                = pe.PreguntaId,
                    Orden                     = pe.Orden,
                    TextoPregunta             = pe.Pregunta.TextoPregunta,
                    Alternativas              = altsOrdenadas,
                    AlternativaSeleccionadaId = pe.AlternativaSeleccionadaId
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
                .FirstOrDefaultAsync(e => e.Id == examenId && e.UsuarioId == CurrentUserId && !e.Completado);

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
                .FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == CurrentUserId && e.Completado);

            if (examen == null)
                return RedirectToAction(nameof(Historial));

            var notaPonderada = await _db.Estudiantes
                .Where(u => u.Id == CurrentUserId)
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
                    EsCorrecta            = pe.AlternativaSeleccionada?.EsCorrecta ?? false
                });
            }

            return View(vm);
        }

        // ── GET /Examen/Configuracion ─────────────────────────────────
        public async Task<IActionResult> Configuracion()
        {
            var usuario = await _db.Estudiantes.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
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
            var usuario = await _db.Estudiantes.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
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
        public async Task<IActionResult> Noticias(int page = 1)
        {
            const int pageSize = 6;
            var query = _db.Noticias
                .Include(n => n.Admin)
                .Where(n => n.Activo)
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
            return View(noticias);
        }

        // ── GET /Examen/Historial ─────────────────────────────────────
        public async Task<IActionResult> Historial()
        {
            var examenes = await _db.Examenes
                .Include(e => e.TipoExamen)
                .Where(e => e.UsuarioId == CurrentUserId && e.Completado)
                .OrderByDescending(e => e.FechaFin)
                .ToListAsync();

            return View(examenes);
        }

        // ── GET /Examen/Sugerencias ───────────────────────────────────
        public async Task<IActionResult> Sugerencias()
        {
            var uid = CurrentUserId;
            var mis = await _db.Sugerencias
                .Where(s => s.UsuarioId == uid)
                .OrderByDescending(s => s.FechaEnvio)
                .ToListAsync();
            ViewBag.MisSugerencias = mis;
            return View();
        }

        // ── POST /Examen/Sugerencias ──────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sugerencias(string asunto, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(mensaje))
            {
                TempData["Error"] = "El asunto y el mensaje son obligatorios.";
                return RedirectToAction(nameof(Sugerencias));
            }

            _db.Sugerencias.Add(new Sugerencia
            {
                UsuarioId  = CurrentUserId,
                Asunto     = asunto.Trim()[..Math.Min(100, asunto.Trim().Length)],
                Mensaje    = mensaje.Trim()[..Math.Min(2000, mensaje.Trim().Length)],
                FechaEnvio = DateTime.Now,
                Leida      = false
            });
            await _db.SaveChangesAsync();

            TempData["Exito"] = "¡Gracias! Tu sugerencia fue enviada correctamente.";
            return RedirectToAction(nameof(Sugerencias));
        }
    }
}
