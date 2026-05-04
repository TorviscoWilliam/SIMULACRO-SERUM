using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Representa una sesión de examen iniciada por un usuario.
    /// Registra cuándo comenzó, cuándo terminó, el puntaje obtenido y si fue completado.
    /// Las preguntas y las respuestas del usuario se almacenan en PreguntaExamen.
    /// </summary>
    public class Examen
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK al usuario que inició este examen.</summary>
        public int UsuarioId { get; set; }

        /// <summary>Propiedad de navegación: el usuario que realizó el examen.</summary>
        public Usuario Usuario { get; set; } = null!;

        /// <summary>FK al tipo de examen seleccionado por el usuario al iniciar. Null si fue un examen libre sin categoría.</summary>
        [Display(Name = "Tipo de Examen")]
        public int? TipoExamenId { get; set; }

        /// <summary>Propiedad de navegación: el tipo de examen con sus configuraciones.</summary>
        public TipoExamen? TipoExamen { get; set; }

        /// <summary>Fecha y hora en que el usuario inició el examen.</summary>
        [Display(Name = "Inicio")]
        public DateTime FechaInicio { get; set; } = DateTime.Now;

        /// <summary>Fecha y hora en que el usuario entregó el examen. Null si aún no lo ha completado.</summary>
        [Display(Name = "Fin")]
        public DateTime? FechaFin { get; set; }

        /// <summary>Número de respuestas correctas del examen. Se actualiza al momento de la entrega.</summary>
        [Display(Name = "Puntaje")]
        public int Puntaje { get; set; } = 0;

        /// <summary>Cantidad de preguntas que contenía este examen. Se fija al iniciarlo y no varía.</summary>
        [Display(Name = "Total Preguntas")]
        public int TotalPreguntas { get; set; } = 0;

        /// <summary>Indica si el usuario ya entregó el examen. Un examen no completado puede considerarse abandonado.</summary>
        public bool Completado { get; set; } = false;

        /// <summary>Duración máxima en segundos. Null = sin límite de tiempo.</summary>
        public int? DuracionSegundos { get; set; }

        /// <summary>Porcentaje de aciertos redondeado a 1 decimal. Calculado en tiempo real a partir de Puntaje y TotalPreguntas.</summary>
        public double Porcentaje => TotalPreguntas > 0
            ? Math.Round((double)Puntaje / TotalPreguntas * 100, 1)
            : 0;

        /// <summary>Puntaje vigesimal (0–20) proporcional al total de preguntas del examen.</summary>
        public double PuntajeVigesimal => TotalPreguntas > 0
            ? Math.Round((double)Puntaje / TotalPreguntas * 20, 2)
            : 0;

        /// <summary>Colección de detalles de cada pregunta incluida en este examen, con las respuestas del usuario.</summary>
        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
