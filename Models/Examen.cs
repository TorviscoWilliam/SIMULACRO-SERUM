using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class Examen
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Display(Name = "Tipo de Examen")]
        public int? TipoExamenId { get; set; }
        public TipoExamen? TipoExamen { get; set; }

        [Display(Name = "Inicio")]
        public DateTime FechaInicio { get; set; } = DateTime.Now;

        [Display(Name = "Fin")]
        public DateTime? FechaFin { get; set; }

        [Display(Name = "Puntaje")]
        public int Puntaje { get; set; } = 0;

        [Display(Name = "Total Preguntas")]
        public int TotalPreguntas { get; set; } = 0;

        public bool Completado { get; set; } = false;

        public double Porcentaje => TotalPreguntas > 0
            ? Math.Round((double)Puntaje / TotalPreguntas * 100, 1)
            : 0;

        /// <summary>Puntaje vigesimal: cada pregunta correcta vale 0.2 puntos (máx 20 en 100 preguntas).</summary>
        public double PuntajeVigesimal => Math.Round(Puntaje * 0.2, 2);

        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
