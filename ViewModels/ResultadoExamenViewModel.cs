namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel de la vista Examen/Resultado. Consolida el resumen del examen
    /// (puntaje, porcentaje, duración) y el detalle pregunta por pregunta para
    /// que el usuario pueda revisar sus errores y ver la respuesta correcta.
    /// </summary>
    public class ResultadoExamenViewModel
    {
        /// <summary>ID del examen completado. Se usa para el enlace "Ver resultado" del historial.</summary>
        public int      ExamenId        { get; set; }

        /// <summary>Número de respuestas correctas (1 punto cada una).</summary>
        public int      Puntaje         { get; set; }

        /// <summary>Total de preguntas que contenía el examen (puede ser menor que NumeroPreguntas si hay pocas en la BD).</summary>
        public int      TotalPreguntas  { get; set; }

        /// <summary>
        /// Porcentaje de aciertos ya redondeado a 1 decimal.
        /// Copiado desde Examen.Porcentaje (calculado en el modelo de dominio).
        /// </summary>
        public double   Porcentaje      { get; set; }

        /// <summary>Momento en que el usuario inició el examen.</summary>
        public DateTime FechaInicio     { get; set; }

        /// <summary>Momento en que el usuario envió el formulario. Nunca es null aquí (examen completado).</summary>
        public DateTime FechaFin        { get; set; }

        /// <summary>
        /// Tiempo total que tardó el usuario en completar el examen.
        /// Calculado en el ViewModel como FechaFin − FechaInicio (no persiste en la BD).
        /// </summary>
        public TimeSpan Duracion        => FechaFin - FechaInicio;

        /// <summary>
        /// Lista de detalles por pregunta, en el mismo orden en que aparecieron durante el examen.
        /// Permite al usuario ver qué respondió y cuál era la respuesta correcta.
        /// </summary>
        public List<DetalleRespuestaVM> Detalles { get; set; } = new();
    }

    /// <summary>
    /// Detalle de una pregunta específica dentro del resultado del examen.
    /// Muestra el enunciado, la respuesta seleccionada por el usuario y la correcta,
    /// con un indicador visual (EsCorrecta) para facilitar la revisión.
    /// </summary>
    public class DetalleRespuestaVM
    {
        /// <summary>Número secuencial de la pregunta (1, 2, 3...) para mostrar "Pregunta N".</summary>
        public int     Numero               { get; set; }

        /// <summary>Enunciado de la pregunta tal como apareció durante el examen.</summary>
        public string  TextoPregunta        { get; set; } = string.Empty;

        /// <summary>
        /// Texto de la alternativa marcada como EsCorrecta=true en la BD.
        /// Siempre tiene valor ("-" si no se encontró la alternativa, aunque no debería ocurrir).
        /// </summary>
        public string  RespuestaCorrecta    { get; set; } = string.Empty;

        /// <summary>
        /// Texto de la alternativa que seleccionó el usuario.
        /// Null si la pregunta quedó sin responder (no se marcó ningún radio button).
        /// </summary>
        public string? RespuestaSeleccionada { get; set; }

        /// <summary>
        /// true si la respuesta seleccionada fue la correcta.
        /// Se calcula en ExamenController.Enviar() y se persiste en PreguntaExamen.EsCorrecta
        /// para no tener que recalcular en cada consulta del historial.
        /// </summary>
        public bool    EsCorrecta           { get; set; }
    }
}
