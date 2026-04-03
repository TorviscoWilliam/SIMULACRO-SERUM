namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel raíz de la vista Examen/Tomar. Agrupa el ID del examen en curso
    /// y la lista ordenada de preguntas con sus alternativas ya mezcladas.
    /// El campo ExamenId se envía como campo hidden en el formulario POST /Examen/Enviar.
    /// </summary>
    public class ExamenViewModel
    {
        public int    ExamenId         { get; set; }
        public string TipoExamenNombre { get; set; } = string.Empty;
        public List<PreguntaExamenVM> Preguntas { get; set; } = new();
    }

    /// <summary>
    /// Representa una pregunta específica dentro de un examen en curso.
    /// PreguntaExamenId es el ID del registro puente (PreguntaExamen.Id), no de la pregunta
    /// en sí; se usa como clave del campo radio en el formulario: name="respuesta_{PreguntaExamenId}".
    /// </summary>
    public class PreguntaExamenVM
    {
        /// <summary>
        /// ID del registro PreguntaExamen (tabla puente). Se usa para construir
        /// el name del radio button: "respuesta_{PreguntaExamenId}".
        /// </summary>
        public int PreguntaExamenId { get; set; }

        /// <summary>ID de la Pregunta original (para referencia; no se usa en el POST).</summary>
        public int PreguntaId       { get; set; }

        /// <summary>Posición de esta pregunta en el examen (1-based). Muestra "Pregunta 1 de N".</summary>
        public int Orden            { get; set; }

        /// <summary>Enunciado de la pregunta que se muestra al usuario.</summary>
        public string TextoPregunta { get; set; } = string.Empty;

        /// <summary>
        /// Alternativas en el orden aleatorio para este examen (ya reordenadas desde OrdenAlternativas).
        /// No incluye EsCorrecta para que el usuario no pueda inspeccionarla en el HTML.
        /// </summary>
        public List<AlternativaVM> Alternativas { get; set; } = new();

        /// <summary>
        /// ID de la alternativa guardada (auto-guardado). Null si no se respondió aún.
        /// Se usa para pre-seleccionar la respuesta al reanudar el examen.
        /// </summary>
        public int? AlternativaSeleccionadaId { get; set; }
    }

    /// <summary>
    /// Alternativa mínima para la vista del examen. Solo expone Id y texto.
    /// El Id se envía como value del radio button y es lo que llega al POST /Examen/Enviar.
    /// EsCorrecta se omite intencionalmente para no revelar la respuesta en el HTML.
    /// </summary>
    public class AlternativaVM
    {
        /// <summary>ID de la alternativa (Alternativa.Id). Se envía como value del radio button.</summary>
        public int    Id                { get; set; }

        /// <summary>Texto que ve el usuario como opción de respuesta.</summary>
        public string TextoAlternativa  { get; set; } = string.Empty;
    }
}
