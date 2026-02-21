namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel alternativo para enviar respuestas con model binding tipado.
    /// NOTA: Este ViewModel no se usa actualmente en ExamenController.Enviar(),
    /// que lee las respuestas directamente de IFormCollection con claves dinámicas
    /// ("respuesta_{pe.Id}") para mayor flexibilidad. Se conserva como alternativa
    /// si se prefiere binding fuertemente tipado en el futuro.
    /// </summary>
    public class SubmitExamenViewModel
    {
        /// <summary>ID del examen que se está enviando (campo hidden del formulario).</summary>
        public int ExamenId { get; set; }

        /// <summary>
        /// Diccionario de respuestas: clave = PreguntaExamenId, valor = AlternativaId seleccionada.
        /// Las preguntas no respondidas no tienen entrada en el diccionario.
        /// </summary>
        public Dictionary<int, int> Respuestas { get; set; } = new();
    }
}
