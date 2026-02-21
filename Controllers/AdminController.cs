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
    /// Controlador exclusivo del rol "Admin".
    /// Gestiona usuarios, preguntas e importación/exportación de Excel.
    /// Todos los métodos heredan la restricción [Authorize(Roles = "Admin")]
    /// definida a nivel de clase.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IExcelService        _excel;

        /// <summary>DbContext e IExcelService inyectados por DI.</summary>
        public AdminController(ApplicationDbContext db, IExcelService excel)
        {
            _db    = db;
            _excel = excel;
        }

        // ── Dashboard ────────────────────────────────────────────────
        /// <summary>
        /// Página principal del administrador con 4 estadísticas globales:
        /// - Total de usuarios activos (rol Usuario).
        /// - Total de preguntas activas en el banco.
        /// - Total de exámenes completados por todos los usuarios.
        /// - Porcentaje promedio global de aciertos.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsuarios  = await _db.Usuarios.CountAsync(u => u.Rol == "Usuario" && u.Activo);
            ViewBag.TotalPreguntas = await _db.Preguntas.CountAsync(p => p.Activo);
            ViewBag.TotalExamenes  = await _db.Examenes.CountAsync(e => e.Completado);
            // (double?) permite que AverageAsync devuelva null si no hay exámenes → ?? 0
            ViewBag.PromedioGlobal = await _db.Examenes
                .Where(e => e.Completado && e.TotalPreguntas > 0)
                .AverageAsync(e => (double?)((double)e.Puntaje / e.TotalPreguntas * 100)) ?? 0;

            return View();
        }

        // ═══════════════════════════════════════════════════════════
        //  USUARIOS
        // ═══════════════════════════════════════════════════════════

        // ── GET /Admin/Usuarios ──────────────────────────────────────
        /// <summary>
        /// Lista todos los usuarios con rol "Usuario" (no muestra admins).
        /// Proyecta con Select() para traer solo los campos necesarios
        /// y calcular TotalExamenes y MejorPuntaje directamente en SQL.
        /// </summary>
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
                        : 0
                })
                .ToListAsync();

            return View(usuarios);
        }

        // ── GET /Admin/CrearUsuario ──────────────────────────────────
        /// <summary>Muestra el formulario vacío para crear un nuevo usuario.</summary>
        public IActionResult CrearUsuario() => View(new CrearUsuarioViewModel());

        // ── POST /Admin/CrearUsuario ─────────────────────────────────
        /// <summary>
        /// Crea un usuario con rol "Usuario" (nunca Admin desde este formulario).
        /// Valida unicidad de NombreUsuario y Correo antes de insertar.
        /// La contraseña se hashea con BCrypt antes de guardarse.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearUsuario(CrearUsuarioViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // Verificar unicidad a nivel de aplicación (la BD también tiene índices únicos)
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
                Contrasena    = BCrypt.Net.BCrypt.HashPassword(vm.Contrasena), // Hash seguro con salt
                Rol           = "Usuario",   // Siempre "Usuario"; solo Program.cs crea "Admin"
                FechaCreacion = DateTime.Now,
                Activo        = true
            });

            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Usuario '{vm.NombreUsuario}' creado correctamente.";
            return RedirectToAction(nameof(Usuarios));
        }

        // ── POST /Admin/ToggleUsuario/{id} ───────────────────────────
        /// <summary>
        /// Activa o desactiva un usuario alternando el campo Activo (soft delete lógico).
        /// Los usuarios Admin no pueden ser desactivados desde esta acción.
        /// Un usuario desactivado no puede iniciar sesión.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUsuario(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            // Protección: no permitir desactivar admins desde la UI de usuarios
            if (u == null || u.Rol == "Admin") return NotFound();

            u.Activo = !u.Activo; // Alterna el estado
            await _db.SaveChangesAsync();

            TempData["Exito"] = u.Activo
                ? $"Usuario '{u.NombreUsuario}' activado."
                : $"Usuario '{u.NombreUsuario}' desactivado.";

            return RedirectToAction(nameof(Usuarios));
        }

        // ── GET /Admin/ExportarUsuarios ──────────────────────────────
        /// <summary>
        /// Genera y descarga un archivo Excel (.xlsx) con todos los usuarios.
        /// El nombre del archivo incluye la fecha/hora para facilitar el control de versiones.
        /// </summary>
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
                        : 0
                })
                .ToListAsync();

            var bytes    = _excel.ExportarUsuarios(usuarios);
            var filename = $"Usuarios_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                filename);
        }

        // ═══════════════════════════════════════════════════════════
        //  PREGUNTAS
        // ═══════════════════════════════════════════════════════════

        // ── GET /Admin/Preguntas ─────────────────────────────────────
        /// <summary>
        /// Lista las preguntas activas con sus alternativas, más recientes primero.
        /// Include() carga las alternativas en la misma consulta (evita N+1 queries).
        /// </summary>
        public async Task<IActionResult> Preguntas()
        {
            var preguntas = await _db.Preguntas
                .Where(p => p.Activo)
                .Include(p => p.Alternativas) // Eager loading: trae alternativas en el mismo SELECT
                .OrderByDescending(p => p.FechaCreacion)
                .ToListAsync();

            return View(preguntas);
        }

        // ── GET /Admin/CrearPregunta ─────────────────────────────────
        /// <summary>Muestra el formulario vacío para crear una nueva pregunta.</summary>
        public IActionResult CrearPregunta() => View(new PreguntaFormViewModel());

        // ── POST /Admin/CrearPregunta ────────────────────────────────
        /// <summary>
        /// Crea una pregunta con sus alternativas a partir del formulario.
        /// Siempre crea: 1 alternativa correcta + 1 incorrecta (mínimo obligatorio).
        /// Opcionalmente agrega hasta 2 alternativas incorrectas adicionales.
        /// EF Core guarda pregunta y alternativas en la misma transacción (gracias a la
        /// relación de navegación: _db.Preguntas.Add(pregunta) incluye sus alternativas).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPregunta(PreguntaFormViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var pregunta = new Pregunta
            {
                TextoPregunta = vm.TextoPregunta,
                FechaCreacion = DateTime.Now,
                Activo        = true
            };

            // Alternativa 1: la correcta (EsCorrecta = true)
            pregunta.Alternativas.Add(new Alternativa
            {
                TextoAlternativa = vm.RespuestaCorrecta,
                EsCorrecta       = true
            });

            // Alternativa 2: incorrecta, obligatoria
            pregunta.Alternativas.Add(new Alternativa
            {
                TextoAlternativa = vm.Opcion2,
                EsCorrecta       = false
            });

            // Alternativas 3 y 4: opcionales
            if (!string.IsNullOrWhiteSpace(vm.Opcion3))
                pregunta.Alternativas.Add(new Alternativa
                {
                    TextoAlternativa = vm.Opcion3,
                    EsCorrecta       = false
                });

            if (!string.IsNullOrWhiteSpace(vm.Opcion4))
                pregunta.Alternativas.Add(new Alternativa
                {
                    TextoAlternativa = vm.Opcion4,
                    EsCorrecta       = false
                });

            _db.Preguntas.Add(pregunta); // EF Core detecta las alternativas hijas automáticamente
            await _db.SaveChangesAsync();

            TempData["Exito"] = "Pregunta agregada correctamente.";
            return RedirectToAction(nameof(Preguntas));
        }

        // ── POST /Admin/EliminarPregunta/{id} ────────────────────────
        /// <summary>
        /// Realiza un "soft delete": marca la pregunta como inactiva (Activo = false)
        /// en lugar de eliminarla físicamente, para preservar el historial de exámenes.
        /// La pregunta deja de aparecer en el banco y no se incluirá en futuros exámenes.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPregunta(int id)
        {
            var p = await _db.Preguntas.FindAsync(id);
            if (p == null) return NotFound();

            p.Activo = false; // Soft delete: no se borra de la BD
            await _db.SaveChangesAsync();

            TempData["Exito"] = "Pregunta eliminada.";
            return RedirectToAction(nameof(Preguntas));
        }

        // ── POST /Admin/CargarPreguntas (importar Excel) ─────────────
        /// <summary>
        /// Importa preguntas masivamente desde un archivo Excel.
        /// Flujo:
        ///   1. Valida que el archivo exista y sea .xlsx o .xls.
        ///   2. Delega la lectura al ExcelService (columnas A-E).
        ///   3. Descarta filas con datos insuficientes.
        ///   4. Guarda todas las preguntas válidas en una sola llamada SaveChanges().
        /// El try/catch captura errores de formato (archivo corrupto, estructura inesperada).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CargarPreguntas(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                TempData["Error"] = "Seleccione un archivo Excel válido.";
                return RedirectToAction(nameof(Preguntas));
            }

            // Límite de 10 MB para evitar consumo excesivo de memoria al procesar el Excel
            const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
            if (archivo.Length > MaxFileSize)
            {
                TempData["Error"] = "El archivo supera el límite de 10 MB.";
                return RedirectToAction(nameof(Preguntas));
            }

            // Validar extensión del archivo (no se confía solo en el Content-Type)
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
                importadas = _excel.ImportarPreguntas(stream); // ClosedXML lee el archivo
            }
            catch
            {
                // Cualquier excepción de ClosedXML al leer el archivo
                TempData["Error"] = "Error al procesar el archivo. Verifique el formato.";
                return RedirectToAction(nameof(Preguntas));
            }

            if (importadas.Count == 0)
            {
                TempData["Error"] = "No se encontraron preguntas válidas en el archivo.";
                return RedirectToAction(nameof(Preguntas));
            }

            // Construir y agregar cada pregunta con sus alternativas
            int guardadas = 0;
            foreach (var vm in importadas)
            {
                var pregunta = new Pregunta
                {
                    TextoPregunta = vm.TextoPregunta,
                    FechaCreacion = DateTime.Now,
                    Activo        = true
                };

                pregunta.Alternativas.Add(new Alternativa
                    { TextoAlternativa = vm.RespuestaCorrecta, EsCorrecta = true });

                pregunta.Alternativas.Add(new Alternativa
                    { TextoAlternativa = vm.Opcion2, EsCorrecta = false });

                if (!string.IsNullOrWhiteSpace(vm.Opcion3))
                    pregunta.Alternativas.Add(new Alternativa
                        { TextoAlternativa = vm.Opcion3, EsCorrecta = false });

                if (!string.IsNullOrWhiteSpace(vm.Opcion4))
                    pregunta.Alternativas.Add(new Alternativa
                        { TextoAlternativa = vm.Opcion4, EsCorrecta = false });

                _db.Preguntas.Add(pregunta);
                guardadas++;
            }

            await _db.SaveChangesAsync(); // Una sola transacción para todas las preguntas
            TempData["Exito"] = $"Se importaron {guardadas} pregunta(s) correctamente.";
            return RedirectToAction(nameof(Preguntas));
        }

        // ── GET /Admin/DescargarPlantilla ────────────────────────────
        /// <summary>
        /// Genera y descarga la plantilla Excel de ejemplo para importar preguntas.
        /// Incluye encabezados con asteriscos (*) para indicar columnas obligatorias
        /// y dos filas de ejemplo para guiar al administrador.
        /// </summary>
        public IActionResult DescargarPlantilla()
        {
            var bytes = _excel.GenerarPlantillaPreguntas();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Plantilla_Preguntas.xlsx");
        }
    }
}
