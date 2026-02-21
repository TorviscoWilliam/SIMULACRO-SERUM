using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Representa una pregunta del banco de preguntas del sistema.
    /// Cada pregunta tiene al menos 2 alternativas (1 correcta + 1 incorrecta)
    /// y puede tener hasta 4 alternativas en total.
    /// Al ser incluida en un examen, su orden y el de sus alternativas se aleatoriza.
    /// </summary>
    public class Pregunta
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>
        /// Enunciado de la pregunta. Almacenado como nvarchar(max) para
        /// soportar textos de cualquier longitud.
        /// </summary>
        [Required(ErrorMessage = "El texto de la pregunta es obligatorio")]
        [Display(Name = "Pregunta")]
        public string TextoPregunta { get; set; } = string.Empty;

        /// <summary>Fecha y hora en que el administrador registró la pregunta.</summary>
        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Soft delete: cuando es false la pregunta no aparece en los exámenes.
        /// Se usa en lugar de borrar físicamente el registro para preservar el
        /// historial de exámenes anteriores que la usaron.
        /// </summary>
        public bool Activo { get; set; } = true;

        // ── Propiedades de navegación EF Core ───────────────────────
        /// <summary>
        /// Alternativas de esta pregunta (mínimo 2, máximo 4).
        /// EF Core aplica Cascade Delete: al eliminar la pregunta se eliminan sus alternativas.
        /// </summary>
        public ICollection<Alternativa> Alternativas { get; set; } = new List<Alternativa>();

        /// <summary>Registros de los exámenes en que esta pregunta fue incluida.</summary>
        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
