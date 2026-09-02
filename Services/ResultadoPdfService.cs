using System.Text;
using SimulacroExamen.ViewModels;

namespace SimulacroExamen.Services
{
    /// <summary>
    /// Genera un PDF simple con fuentes estandar de PDF para exportar resultados.
    /// No depende de librerias externas: suficiente para reportes tabulares de texto.
    /// </summary>
    public static class ResultadoPdfService
    {
        private const double PageWidth = 595;  // A4 en puntos
        private const double PageHeight = 842;
        private const double Margin = 42;
        private const double BottomMargin = 42;

        public static byte[] Generar(ResultadoExamenViewModel resultado)
        {
            var pages = new List<StringBuilder>();
            var page = NewPage(pages);
            double y = 800;

            void EnsureSpace(double needed)
            {
                if (y - needed >= BottomMargin) return;
                page = NewPage(pages);
                y = 800;
            }

            void Text(string value, double x, double size = 10, bool bold = false)
            {
                page.AppendLine($"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {x:0.##} {y:0.##} Td ({Escape(value)}) Tj ET");
                y -= size + 5;
            }

            void Wrapped(string value, double x, int maxChars, double size = 10, bool bold = false)
            {
                foreach (var line in Wrap(value, maxChars))
                {
                    EnsureSpace(size + 7);
                    Text(line, x, size, bold);
                }
            }

            Text("Resultado del simulacro", Margin, 18, true);
            Text(string.IsNullOrWhiteSpace(resultado.TipoExamenNombre) ? "Examen" : resultado.TipoExamenNombre, Margin, 12);
            y -= 8;

            Text($"Examen ID: {resultado.ExamenId}", Margin, 10, true);
            Text($"Fecha inicio: {resultado.FechaInicio:dd/MM/yyyy HH:mm}", Margin);
            Text($"Fecha fin: {resultado.FechaFin:dd/MM/yyyy HH:mm}", Margin);
            Text($"Duracion: {(int)resultado.Duracion.TotalMinutes}m {resultado.Duracion.Seconds}s", Margin);
            y -= 6;

            Text($"Puntaje: {resultado.Puntaje}/{resultado.TotalPreguntas}", Margin, 12, true);
            Text($"Nota vigesimal: {resultado.PuntajeVigesimal:0.##}/20", Margin, 12, true);
            Text($"Porcentaje: {resultado.Porcentaje:0.0}%", Margin, 12, true);
            if (resultado.NotaFinal.HasValue)
            {
                Text($"Nota ponderada: {resultado.NotaPonderada:0.##}/20", Margin);
                Text($"Nota final: {resultado.NotaFinal:0.##}/20", Margin, 12, true);
            }

            y -= 12;
            Text("Revision de respuestas", Margin, 14, true);

            foreach (var detalle in resultado.Detalles)
            {
                EnsureSpace(72);
                Text($"{detalle.Numero}. {(detalle.EsCorrecta ? "Correcta" : "Incorrecta")}", Margin, 11, true);
                Wrapped($"Pregunta: {detalle.TextoPregunta}", Margin + 12, 92);
                Wrapped($"Tu respuesta: {detalle.RespuestaSeleccionada ?? "Sin responder"}", Margin + 12, 92);
                Wrapped($"Respuesta correcta: {detalle.RespuestaCorrecta}", Margin + 12, 92);
                y -= 5;
            }

            return BuildPdf(pages);
        }

        private static StringBuilder NewPage(List<StringBuilder> pages)
        {
            var sb = new StringBuilder();
            pages.Add(sb);
            return sb;
        }

        private static IEnumerable<string> Wrap(string text, int maxChars)
        {
            text = Sanitize(text);
            if (text.Length <= maxChars)
            {
                yield return text;
                yield break;
            }

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = new StringBuilder();
            foreach (var word in words)
            {
                if (line.Length > 0 && line.Length + word.Length + 1 > maxChars)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                if (word.Length > maxChars)
                {
                    if (line.Length > 0)
                    {
                        yield return line.ToString();
                        line.Clear();
                    }

                    for (int i = 0; i < word.Length; i += maxChars)
                        yield return word.Substring(i, Math.Min(maxChars, word.Length - i));
                    continue;
                }

                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }

            if (line.Length > 0)
                yield return line.ToString();
        }

        private static string Escape(string value) =>
            Sanitize(value)
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)");

        private static string Sanitize(string value)
        {
            var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
                sb.Append(ch <= 255 ? ch : '?');
            return sb.ToString();
        }

        private static byte[] BuildPdf(List<StringBuilder> pages)
        {
            var encoding = Encoding.Latin1;
            using var ms = new MemoryStream();
            var offsets = new List<long> { 0 };

            void Write(string value)
            {
                var bytes = encoding.GetBytes(value);
                ms.Write(bytes, 0, bytes.Length);
            }

            void Obj(int number, string body)
            {
                offsets.Add(ms.Position);
                Write($"{number} 0 obj\n{body}\nendobj\n");
            }

            Write("%PDF-1.4\n");

            int pageCount = pages.Count;
            int objectCount = 2 + pageCount * 2;
            var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{3 + i * 2} 0 R"));

            Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
            Obj(2, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");

            for (int i = 0; i < pageCount; i++)
            {
                int pageObj = 3 + i * 2;
                int contentObj = pageObj + 1;
                Obj(pageObj,
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0} {PageHeight:0}] " +
                    $"/Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> " +
                    $"/F2 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> >> >> " +
                    $"/Contents {contentObj} 0 R >>");

                var content = pages[i].ToString();
                var contentBytes = encoding.GetBytes(content);
                offsets.Add(ms.Position);
                Write($"{contentObj} 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
                ms.Write(contentBytes, 0, contentBytes.Length);
                Write("endstream\nendobj\n");
            }

            long xref = ms.Position;
            Write($"xref\n0 {objectCount + 1}\n");
            Write("0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1))
                Write($"{offset:0000000000} 00000 n \n");

            Write($"trailer\n<< /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
            return ms.ToArray();
        }
    }
}
