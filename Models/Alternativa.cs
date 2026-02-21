using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Representa una opción de respuesta asociada a una Pregunta.
    /// Exactamente una alternativa de cada pregunta debe tener EsCorrecta = true.
    /// Al mostrar el examen, las alternativas se presentan en orden aleatorio
    /// (guardado en PreguntaExamen.OrdenAlternativas) para que no sea predecible.
    /// </summary>
    public class Alternativa
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK a la pregunta a la que pertenece esta alternativa.</summary>
        public int PreguntaId { get; set; }

        /// <summary>Propiedad de navegación: la pregunta dueña de esta alternativa.</summary>
        public Pregunta Pregunta { get; set; } = null!;

        /// <summary>Texto que verá el usuario como opción de respuesta.</summary>
        [Required(ErrorMessage = "El texto de la alternativa es obligatorio")]
        [Display(Name = "Texto")]
        public string TextoAlternativa { get; set; } = string.Empty;

        /// <summary>
        /// Indica si esta es la respuesta correcta de su pregunta.
        /// Solo debe haber una alternativa con EsCorrecta=true por pregunta.
        /// Al calificar el examen: si la alternativa seleccionada es correcta → +1 punto.
        /// </summary>
        [Display(Name = "Es Correcta")]
        public bool EsCorrecta { get; set; } = false;

        // ── Navegación inversa EF Core ───────────────────────────────
        /// <summary>
        /// Registros de examen donde esta alternativa fue la opción seleccionada por el usuario.
        /// </summary>
        public ICollection<PreguntaExamen> PreguntasExamen { get; set; } = new List<PreguntaExamen>();
    }
}
