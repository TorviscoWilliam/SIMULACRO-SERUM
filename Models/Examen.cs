using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Representa un intento de examen de un usuario.
    /// Flujo de vida: IniciarExamen() crea el registro con Completado=false,
    /// Enviar() lo completa calculando el puntaje y registrando FechaFin.
    /// Cada intento es independiente y queda permanentemente en el historial.
    /// </summary>
    public class Examen
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK al usuario que realizó este examen.</summary>
        public int UsuarioId { get; set; }

        /// <summary>Propiedad de navegación: el usuario dueño de este examen.</summary>
        public Usuario Usuario { get; set; } = null!;

        /// <summary>Momento exacto en que el usuario inició el examen.</summary>
        [Display(Name = "Inicio")]
        public DateTime FechaInicio { get; set; } = DateTime.Now;

        /// <summary>
        /// Momento en que el usuario envió el examen. Null si aún no lo completó.
        /// La diferencia FechaFin - FechaInicio = duración del examen.
        /// </summary>
        [Display(Name = "Fin")]
        public DateTime? FechaFin { get; set; }

        /// <summary>
        /// Número de respuestas correctas. Se calcula en ExamenController.Enviar().
        /// Cada pregunta correcta suma exactamente 1 punto.
        /// </summary>
        [Display(Name = "Puntaje")]
        public int Puntaje { get; set; } = 0;

        /// <summary>
        /// Cantidad de preguntas incluidas en este examen.
        /// Puede ser menor que el total del banco si hay pocas preguntas activas.
        /// </summary>
        [Display(Name = "Total Preguntas")]
        public int TotalPreguntas { get; set; } = 0;

        /// <summary>
        /// true cuando el usuario envió el formulario y el examen fue calificado.
        /// false significa examen en progreso (no se muestra en el historial).
        /// </summary>
        public bool Completado { get; set; } = false;

        /// <summary>
        /// Porcentaje de aciertos calculado en memoria (no persiste en la BD).
        /// EF Core lo ignora mediante Ignore() en OnModelCreating.
        /// Fórmula: (Puntaje / TotalPreguntas) * 100, redondeado a 1 decimal.
        /// </summary>
        public double Porcentaje => TotalPreguntas > 0
            ? Math.Round((double)Puntaje / TotalPreguntas * 100, 1)
            : 0;

        // ── Navegación EF Core ───────────────────────────────────────
        /// <summary>
        /// Detalle de cada pregunta incluida en este examen:
        /// qué alternativa seleccionó el usuario y si fue correcta.
        /// </summary>
        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
