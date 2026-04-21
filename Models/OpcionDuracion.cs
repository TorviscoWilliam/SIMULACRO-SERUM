using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class OpcionDuracion
    {
        public int Id { get; set; }

        public int TipoExamenId { get; set; }
        public TipoExamen TipoExamen { get; set; } = null!;

        [Required(ErrorMessage = "La etiqueta es obligatoria")]
        [MaxLength(100)]
        [Display(Name = "Etiqueta")]
        public string Etiqueta { get; set; } = string.Empty;

        /// <summary>
        /// Duración en minutos. 0 = sin límite de tiempo.
        /// </summary>
        [Range(0, 600, ErrorMessage = "Debe ser entre 0 y 600 minutos (0 = sin tiempo)")]
        [Display(Name = "Duración (minutos)")]
        public int DuracionMinutos { get; set; }

        /// <summary>
        /// Número de preguntas para esta opción. 0 = usar el valor de TipoExamen.NumeroPreguntas.
        /// </summary>
        [Range(0, 500, ErrorMessage = "Debe ser entre 0 y 500")]
        [Display(Name = "N° de Preguntas")]
        public int NumeroPreguntas { get; set; } = 0;

        public int Orden { get; set; } = 0;
    }
}
