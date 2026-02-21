using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class Alternativa
    {
        public int Id { get; set; }

        public int PreguntaId { get; set; }
        public Pregunta Pregunta { get; set; } = null!;

        [Required(ErrorMessage = "El texto de la alternativa es obligatorio")]
        [Display(Name = "Texto")]
        public string TextoAlternativa { get; set; } = string.Empty;

        [Display(Name = "Es Correcta")]
        public bool EsCorrecta { get; set; } = false;

        // Navegación inversa
        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
