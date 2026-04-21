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

        [Required]
        [Range(1, 600, ErrorMessage = "Debe ser entre 1 y 600 minutos")]
        [Display(Name = "Duración (minutos)")]
        public int DuracionMinutos { get; set; }

        public int Orden { get; set; } = 0;
    }
}
