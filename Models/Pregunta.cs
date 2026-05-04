using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Representa una pregunta del banco de preguntas del sistema.
    /// Cada pregunta pertenece a un TipoExamen y tiene entre 2 y 4 alternativas,
    /// exactamente una de las cuales es correcta. Al iniciar un examen se seleccionan
    /// preguntas aleatoriamente del banco y se registran en PreguntaExamen.
    /// </summary>
    public class Pregunta
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>Enunciado de la pregunta que verá el usuario durante el examen.</summary>
        [Required(ErrorMessage = "El texto de la pregunta es obligatorio")]
        [Display(Name = "Pregunta")]
        public string TextoPregunta { get; set; } = string.Empty;

        /// <summary>FK al tipo de examen al que pertenece esta pregunta. Null = sin categoría asignada.</summary>
        [Display(Name = "Tipo de Examen")]
        public int? TipoExamenId { get; set; }

        /// <summary>Propiedad de navegación: el tipo de examen que agrupa esta pregunta.</summary>
        public TipoExamen? TipoExamen { get; set; }

        /// <summary>Fecha y hora en que el administrador creó la pregunta en el sistema.</summary>
        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>Controla si la pregunta está disponible para ser incluida en nuevos exámenes. false = descartada sin eliminar.</summary>
        public bool Activo { get; set; } = true;

        /// <summary>Colección de alternativas (opciones de respuesta) de esta pregunta.</summary>
        public ICollection<Alternativa> Alternativas { get; set; } = new List<Alternativa>();

        /// <summary>Registros de los exámenes en que ha aparecido esta pregunta, con las respuestas dadas por cada usuario.</summary>
        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
