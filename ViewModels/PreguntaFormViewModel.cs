using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel compartido para dos flujos:
    ///   1. Formulario Admin → Crear Pregunta (POST AdminController.CrearPregunta).
    ///   2. Resultado de importación desde Excel (ExcelService.ImportarPreguntas).
    /// El diseño asegura exactamente una respuesta correcta + mínimo una incorrecta.
    /// Opcion3 y Opcion4 son opcionales; si se dejan vacíos, no se crean alternativas.
    /// </summary>
    public class PreguntaFormViewModel
    {
        /// <summary>Enunciado de la pregunta. nvarchar(max) en la BD; no tiene límite de longitud.</summary>
        [Required(ErrorMessage = "El texto de la pregunta es obligatorio")]
        [Display(Name = "Pregunta")]
        public string TextoPregunta { get; set; } = string.Empty;

        /// <summary>
        /// Texto de la única alternativa correcta. Se guarda con EsCorrecta=true.
        /// Al mostrar el examen, esta alternativa aparece mezclada con las incorrectas.
        /// </summary>
        [Required(ErrorMessage = "La respuesta correcta es obligatoria")]
        [Display(Name = "Respuesta Correcta")]
        public string RespuestaCorrecta { get; set; } = string.Empty;

        /// <summary>
        /// Primera alternativa incorrecta. Obligatoria: toda pregunta debe tener al
        /// menos dos opciones para que el examen tenga sentido.
        /// </summary>
        [Required(ErrorMessage = "Al menos una opción incorrecta es obligatoria")]
        [Display(Name = "Opción 2")]
        public string Opcion2 { get; set; } = string.Empty;

        /// <summary>
        /// Segunda alternativa incorrecta opcional. Si se deja vacío (null/whitespace),
        /// no se crea el registro Alternativa correspondiente.
        /// </summary>
        [Display(Name = "Opción 3")]
        public string? Opcion3 { get; set; }

        /// <summary>
        /// Tercera alternativa incorrecta opcional. Solo disponible si Opcion3 también
        /// fue completada, aunque la validación no lo exige explícitamente.
        /// </summary>
        [Display(Name = "Opción 4")]
        public string? Opcion4 { get; set; }
    }
}
