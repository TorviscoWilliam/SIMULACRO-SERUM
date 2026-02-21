using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class Examen
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Display(Name = "Inicio")]
        public DateTime FechaInicio { get; set; } = DateTime.Now;

        [Display(Name = "Fin")]
        public DateTime? FechaFin { get; set; }

        [Display(Name = "Puntaje")]
        public int Puntaje { get; set; } = 0;

        [Display(Name = "Total Preguntas")]
        public int TotalPreguntas { get; set; } = 0;

        public bool Completado { get; set; } = false;

        // Calculado
        public double Porcentaje => TotalPreguntas > 0
            ? Math.Round((double)Puntaje / TotalPreguntas * 100, 1)
            : 0;

        // Navegación
        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
