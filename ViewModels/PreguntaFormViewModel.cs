using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class PreguntaFormViewModel
    {
        [Required(ErrorMessage = "El texto de la pregunta es obligatorio")]
        [Display(Name = "Pregunta")]
        public string TextoPregunta { get; set; } = string.Empty;

        [Required(ErrorMessage = "La respuesta correcta es obligatoria")]
        [Display(Name = "Respuesta Correcta")]
        public string RespuestaCorrecta { get; set; } = string.Empty;

        [Required(ErrorMessage = "Al menos una opción incorrecta es obligatoria")]
        [Display(Name = "Opción 2")]
        public string Opcion2 { get; set; } = string.Empty;

        [Display(Name = "Opción 3")]
        public string? Opcion3 { get; set; }

        [Display(Name = "Opción 4")]
        public string? Opcion4 { get; set; }
    }
}
