using ClosedXML.Excel;
using SimulacroExamen.ViewModels;

namespace SimulacroExamen.Services
{
    /// <summary>
    /// Implementación concreta de IExcelService usando la librería ClosedXML.
    /// Registrada como Scoped en Program.cs → una instancia por solicitud HTTP.
    /// </summary>
    public class ExcelService : IExcelService
    {
        // ── Importar preguntas desde Excel ───────────────────────────
        /// <summary>
        /// Lee un archivo Excel (.xlsx/.xls) y extrae preguntas del formato esperado:
        ///   Columna A: Texto de la pregunta (obligatorio).
        ///   Columna B: Respuesta correcta (obligatorio).
        ///   Columna C: Opción incorrecta 2 (obligatorio).
        ///   Columna D: Opción incorrecta 3 (opcional).
        ///   Columna E: Opción incorrecta 4 (opcional).
        /// La fila 1 se omite (es el encabezado). Las filas con pregunta o
        /// respuesta correcta vacías son descartadas silenciosamente.
        /// </summary>
        /// <param name="stream">Stream del archivo Excel abierto por el controller.</param>
        /// <returns>Lista de ViewModels listos para persistir en la BD.</returns>
        public List<PreguntaFormViewModel> ImportarPreguntas(Stream stream)
        {
            var lista = new List<PreguntaFormViewModel>();

            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1); // Siempre se lee la primera hoja

            // La fila 1 es el encabezado; los datos empiezan en fila 2
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int row = 2; row <= lastRow; row++)
            {
                var textoPregunta = ws.Cell(row, 1).GetString().Trim();
                var respCorrecta  = ws.Cell(row, 2).GetString().Trim();

                // Descartar filas incompletas (mínimo obligatorio: pregunta y respuesta correcta)
                if (string.IsNullOrEmpty(textoPregunta) || string.IsNullOrEmpty(respCorrecta))
                    continue;

                var vm = new PreguntaFormViewModel
                {
                    TextoPregunta     = textoPregunta,
                    RespuestaCorrecta = respCorrecta,
                    Opcion2           = ws.Cell(row, 3).GetString().Trim(),
                    Opcion3           = ws.Cell(row, 4).GetString().Trim().NullIfEmpty(), // null si vacío
                    Opcion4           = ws.Cell(row, 5).GetString().Trim().NullIfEmpty(), // null si vacío
                };

                if (string.IsNullOrEmpty(vm.Opcion2))
                    continue; // Necesita al menos una alternativa incorrecta

                lista.Add(vm);
            }

            return lista;
        }

        // ── Exportar usuarios a Excel ────────────────────────────────
        /// <summary>
        /// Genera un archivo Excel con todos los usuarios en formato tabla:
        ///   - Fila 1: encabezados con fondo oscuro y texto blanco.
        ///   - Filas pares: fondo gris claro alternado para facilitar la lectura.
        ///   - Primera fila congelada (freeze pane) para facilitar el scroll.
        ///   - Columnas ajustadas automáticamente al contenido (AdjustToContents).
        /// </summary>
        /// <param name="usuarios">Lista proyectada desde la BD por AdminController.</param>
        /// <returns>Bytes del archivo .xlsx listos para devolver como FileResult.</returns>
        public byte[] ExportarUsuarios(List<UsuarioListaViewModel> usuarios)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Usuarios");

            // Encabezados de la tabla exportada
            string[] headers = {
                "ID", "Usuario", "Correo", "Rol",
                "Fecha Creación", "Tiempo Registrado",
                "Total Exámenes", "Mejor Puntaje", "Estado"
            };

            // Aplicar estilo a los encabezados (fondo oscuro, texto blanco, centrado)
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2c3e50");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Rellenar filas de datos, una por usuario
            for (int i = 0; i < usuarios.Count; i++)
            {
                int row = i + 2; // +2 porque la fila 1 es el encabezado
                var u   = usuarios[i];

                ws.Cell(row, 1).Value = u.Id;
                ws.Cell(row, 2).Value = u.NombreUsuario;
                ws.Cell(row, 3).Value = u.Correo;
                ws.Cell(row, 4).Value = u.Rol;
                ws.Cell(row, 5).Value = u.FechaCreacion.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 6).Value = u.TiempoFormateado; // Calculado en el ViewModel
                ws.Cell(row, 7).Value = u.TotalExamenes;
                ws.Cell(row, 8).Value = u.MejorPuntaje;
                ws.Cell(row, 9).Value = u.Activo ? "Activo" : "Inactivo";

                // Filas alternas con fondo gris claro para mejorar legibilidad
                if (i % 2 == 1)
                {
                    ws.Row(row).Cells(1, headers.Length)
                      .Style.Fill.BackgroundColor = XLColor.FromHtml("#ecf0f1");
                }
            }

            ws.Columns().AdjustToContents(); // Ajuste automático de ancho de columnas
            ws.SheetView.FreezeRows(1);      // Congelar primera fila (encabezados)

            // Escribir en memoria y devolver como array de bytes
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── Plantilla para importar preguntas ────────────────────────
        /// <summary>
        /// Genera una plantilla Excel vacía con los encabezados correctos y dos
        /// filas de ejemplo para guiar al administrador al cargar preguntas.
        /// Los campos obligatorios llevan asterisco (*) en el encabezado.
        /// El fondo verde (#27ae60) destaca que es la plantilla de carga.
        /// </summary>
        /// <returns>Bytes del archivo .xlsx de la plantilla.</returns>
        public byte[] GenerarPlantillaPreguntas()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Preguntas");

            // Encabezados: los marcados con * son obligatorios al importar
            string[] headers = {
                "Pregunta *", "Respuesta Correcta *",
                "Opción Incorrecta 2 *", "Opción Incorrecta 3", "Opción Incorrecta 4"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#27ae60"); // Verde
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Fila de ejemplo 1: pregunta con 4 alternativas
            ws.Cell(2, 1).Value = "¿Cuál es la capital de Perú?";
            ws.Cell(2, 2).Value = "Lima";
            ws.Cell(2, 3).Value = "Cusco";
            ws.Cell(2, 4).Value = "Arequipa";
            ws.Cell(2, 5).Value = "Trujillo";

            // Fila de ejemplo 2: pregunta con solo 3 alternativas (Opción 4 vacía)
            ws.Cell(3, 1).Value = "¿En qué año se fundó Lima?";
            ws.Cell(3, 2).Value = "1535";
            ws.Cell(3, 3).Value = "1521";
            ws.Cell(3, 4).Value = "1548";
            ws.Cell(3, 5).Value = ""; // Columna opcional puede ir vacía

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Extensión local de string usada en ImportarPreguntas().
    /// Convierte cadenas vacías/whitespace a null para simplificar
    /// las validaciones de opciones opcionales de alternativas.
    /// </summary>
    internal static class StringExtensions
    {
        public static string? NullIfEmpty(this string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
