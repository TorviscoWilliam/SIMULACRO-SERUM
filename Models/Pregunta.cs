using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class Pregunta
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El texto de la pregunta es obligatorio")]
        [Display(Name = "Pregunta")]
        public string TextoPregunta { get; set; } = string.Empty;

        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public bool Activo { get; set; } = true;

        // Navegación
        public ICollection<Alternativa> Alternativas { get; set; } = new List<Alternativa>();
        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
