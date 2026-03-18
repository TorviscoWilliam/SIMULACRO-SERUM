using ClosedXML.Excel;
using SimulacroExamen.Models;
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
        /// Lee un archivo Excel con el formato:
        ///   Col A: Número (se ignora)
        ///   Col B: La pregunta (obligatorio)
        ///   Col C: Opción A (obligatorio)
        ///   Col D: Opción B (obligatorio)
        ///   Col E: Opción C (opcional)
        ///   Col F: Opción D (opcional)
        ///   Col G: Respuesta correcta — letra A, B, C o D (obligatorio)
        /// La fila 1 se omite (encabezado).
        /// </summary>
        public List<PreguntaFormViewModel> ImportarPreguntas(Stream stream)
        {
            var lista = new List<PreguntaFormViewModel>();

            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int row = 2; row <= lastRow; row++)
            {
                var textoPregunta = ws.Cell(row, 2).GetString().Trim(); // Col B
                var opcionA       = ws.Cell(row, 3).GetString().Trim(); // Col C
                var opcionB       = ws.Cell(row, 4).GetString().Trim(); // Col D
                var opcionC       = ws.Cell(row, 5).GetString().Trim(); // Col E (opcional)
                var opcionD       = ws.Cell(row, 6).GetString().Trim(); // Col F (opcional)
                var letraCorrecta = ws.Cell(row, 7).GetString().Trim().ToUpperInvariant(); // Col G

                // Campos mínimos obligatorios
                if (string.IsNullOrEmpty(textoPregunta) ||
                    string.IsNullOrEmpty(opcionA)       ||
                    string.IsNullOrEmpty(opcionB)       ||
                    string.IsNullOrEmpty(letraCorrecta))
                    continue;

                // Determinar cuál opción es la correcta según la letra
                string? respCorrecta = letraCorrecta switch
                {
                    "A" => opcionA,
                    "B" => opcionB,
                    "C" => string.IsNullOrEmpty(opcionC) ? null : opcionC,
                    "D" => string.IsNullOrEmpty(opcionD) ? null : opcionD,
                    _   => null
                };

                if (respCorrecta == null) continue; // letra inválida o apunta a opción vacía

                // Construir lista de incorrectas (todas las opciones excepto la correcta)
                var opciones = new List<(string letra, string texto)>
                {
                    ("A", opcionA),
                    ("B", opcionB),
                };
                if (!string.IsNullOrEmpty(opcionC)) opciones.Add(("C", opcionC));
                if (!string.IsNullOrEmpty(opcionD)) opciones.Add(("D", opcionD));

                var incorrectas = opciones
                    .Where(o => o.letra != letraCorrecta)
                    .Select(o => o.texto)
                    .ToList();

                if (incorrectas.Count == 0) continue;

                lista.Add(new PreguntaFormViewModel
                {
                    TextoPregunta     = textoPregunta,
                    RespuestaCorrecta = respCorrecta,
                    Opcion2           = incorrectas.ElementAtOrDefault(0) ?? "",
                    Opcion3           = incorrectas.ElementAtOrDefault(1).NullIfEmpty(),
                    Opcion4           = incorrectas.ElementAtOrDefault(2).NullIfEmpty(),
                });
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

            // Encabezados (mismo orden que el Excel de importación)
            string[] headers = {
                "Número", "La pregunta *",
                "Opción A *", "Opción B *", "Opción C", "Opción D",
                "Respuesta correcta *"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#27ae60");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Fila ejemplo 1: 4 opciones, respuesta A
            ws.Cell(2, 1).Value = 1;
            ws.Cell(2, 2).Value = "¿Cuál es la capital de Perú?";
            ws.Cell(2, 3).Value = "Lima";
            ws.Cell(2, 4).Value = "Cusco";
            ws.Cell(2, 5).Value = "Arequipa";
            ws.Cell(2, 6).Value = "Trujillo";
            ws.Cell(2, 7).Value = "A";

            // Fila ejemplo 2: 3 opciones (D vacía), respuesta B
            ws.Cell(3, 1).Value = 2;
            ws.Cell(3, 2).Value = "¿En qué año se fundó Lima?";
            ws.Cell(3, 3).Value = "1521";
            ws.Cell(3, 4).Value = "1535";
            ws.Cell(3, 5).Value = "1548";
            ws.Cell(3, 6).Value = ""; // Opción D opcional
            ws.Cell(3, 7).Value = "B";

            // Nota aclaratoria en la fila 5
            var nota = ws.Cell(5, 1);
            nota.Value = "NOTA: La columna 'Respuesta correcta' debe contener la letra A, B, C o D que indica la opción correcta.";
            nota.Style.Font.Italic = true;
            nota.Style.Font.FontColor = XLColor.FromHtml("#c0392b");
            ws.Range(5, 1, 5, 7).Merge();

            ws.Column(2).Width = 50; // La pregunta más ancha
            ws.Columns(1, 1).Width  = 10;
            ws.Columns(3, 6).Width  = 20;
            ws.Column(7).Width = 18;
            ws.SheetView.FreezeRows(1);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── Exportar preguntas a Excel ───────────────────────────────
        public byte[] ExportarPreguntas(List<Pregunta> preguntas)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Preguntas");

            string[] headers = {
                "ID", "Tipo de Examen", "Pregunta",
                "Resp. Correcta", "Opción 2", "Opción 3", "Opción 4",
                "Fecha Creación"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a6b3a");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int i = 0; i < preguntas.Count; i++)
            {
                int row = i + 2;
                var p = preguntas[i];
                var correcta = p.Alternativas.FirstOrDefault(a => a.EsCorrecta);
                var incorrectas = p.Alternativas.Where(a => !a.EsCorrecta).ToList();

                ws.Cell(row, 1).Value = p.Id;
                ws.Cell(row, 2).Value = p.TipoExamen?.Nombre ?? "Sin tipo";
                ws.Cell(row, 3).Value = p.TextoPregunta;
                ws.Cell(row, 4).Value = correcta?.TextoAlternativa ?? "";
                ws.Cell(row, 5).Value = incorrectas.ElementAtOrDefault(0)?.TextoAlternativa ?? "";
                ws.Cell(row, 6).Value = incorrectas.ElementAtOrDefault(1)?.TextoAlternativa ?? "";
                ws.Cell(row, 7).Value = incorrectas.ElementAtOrDefault(2)?.TextoAlternativa ?? "";
                ws.Cell(row, 8).Value = p.FechaCreacion.ToString("dd/MM/yyyy HH:mm");

                if (i % 2 == 1)
                    ws.Row(row).Cells(1, headers.Length)
                      .Style.Fill.BackgroundColor = XLColor.FromHtml("#ecf0f1");
            }

            ws.Column(3).Width = 55;
            ws.Column(4).Width = 30;
            ws.Columns(5, 7).Width = 25;
            ws.Columns(1, 2).AdjustToContents();
            ws.Column(8).AdjustToContents();
            ws.SheetView.FreezeRows(1);

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
