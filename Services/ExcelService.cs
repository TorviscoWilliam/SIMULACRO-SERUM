using ClosedXML.Excel;
using SimulacroExamen.ViewModels;

namespace SimulacroExamen.Services
{
    public class ExcelService : IExcelService
    {
        // ── Importar preguntas desde Excel ───────────────────────────
        public List<PreguntaFormViewModel> ImportarPreguntas(Stream stream)
        {
            var lista = new List<PreguntaFormViewModel>();

            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);

            // La fila 1 es el encabezado; los datos empiezan en fila 2
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int row = 2; row <= lastRow; row++)
            {
                var textoPregunta = ws.Cell(row, 1).GetString().Trim();
                var respCorrecta  = ws.Cell(row, 2).GetString().Trim();

                if (string.IsNullOrEmpty(textoPregunta) || string.IsNullOrEmpty(respCorrecta))
                    continue;

                var vm = new PreguntaFormViewModel
                {
                    TextoPregunta   = textoPregunta,
                    RespuestaCorrecta = respCorrecta,
                    Opcion2         = ws.Cell(row, 3).GetString().Trim(),
                    Opcion3         = ws.Cell(row, 4).GetString().Trim().NullIfEmpty(),
                    Opcion4         = ws.Cell(row, 5).GetString().Trim().NullIfEmpty(),
                };

                if (string.IsNullOrEmpty(vm.Opcion2))
                    continue; // Necesita al menos una alternativa incorrecta

                lista.Add(vm);
            }

            return lista;
        }

        // ── Exportar usuarios a Excel ────────────────────────────────
        public byte[] ExportarUsuarios(List<UsuarioListaViewModel> usuarios)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Usuarios");

            // Encabezados
            string[] headers = {
                "ID", "Usuario", "Correo", "Rol",
                "Fecha Creación", "Tiempo Registrado",
                "Total Exámenes", "Mejor Puntaje", "Estado"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2c3e50");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Datos
            for (int i = 0; i < usuarios.Count; i++)
            {
                int row = i + 2;
                var u   = usuarios[i];

                ws.Cell(row, 1).Value = u.Id;
                ws.Cell(row, 2).Value = u.NombreUsuario;
                ws.Cell(row, 3).Value = u.Correo;
                ws.Cell(row, 4).Value = u.Rol;
                ws.Cell(row, 5).Value = u.FechaCreacion.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 6).Value = u.TiempoFormateado;
                ws.Cell(row, 7).Value = u.TotalExamenes;
                ws.Cell(row, 8).Value = u.MejorPuntaje;
                ws.Cell(row, 9).Value = u.Activo ? "Activo" : "Inactivo";

                // Filas alternas
                if (i % 2 == 1)
                {
                    ws.Row(row).Cells(1, headers.Length)
                      .Style.Fill.BackgroundColor = XLColor.FromHtml("#ecf0f1");
                }
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── Plantilla para importar preguntas ────────────────────────
        public byte[] GenerarPlantillaPreguntas()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Preguntas");

            string[] headers = {
                "Pregunta *", "Respuesta Correcta *",
                "Opción Incorrecta 2 *", "Opción Incorrecta 3", "Opción Incorrecta 4"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#27ae60");
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Fila de ejemplo
            ws.Cell(2, 1).Value = "¿Cuál es la capital de Perú?";
            ws.Cell(2, 2).Value = "Lima";
            ws.Cell(2, 3).Value = "Cusco";
            ws.Cell(2, 4).Value = "Arequipa";
            ws.Cell(2, 5).Value = "Trujillo";

            ws.Cell(3, 1).Value = "¿En qué año se fundó Lima?";
            ws.Cell(3, 2).Value = "1535";
            ws.Cell(3, 3).Value = "1521";
            ws.Cell(3, 4).Value = "1548";
            ws.Cell(3, 5).Value = "";

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }

    internal static class StringExtensions
    {
        public static string? NullIfEmpty(this string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
