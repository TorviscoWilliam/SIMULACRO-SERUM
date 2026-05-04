using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Opción de configuración de duración y número de preguntas que el usuario puede seleccionar
    /// antes de iniciar un examen. Cada TipoExamen puede tener múltiples opciones,
    /// permitiendo al usuario elegir, por ejemplo, entre "Examen completo (180 preguntas)" o
    /// "Examen rápido (40 preguntas, 60 minutos)".
    /// </summary>
    public class OpcionDuracion
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK al tipo de examen al que pertenece esta opción de duración.</summary>
        public int TipoExamenId { get; set; }

        /// <summary>Propiedad de navegación: el tipo de examen dueño de esta opción.</summary>
        public TipoExamen TipoExamen { get; set; } = null!;

        /// <summary>Texto descriptivo que verá el usuario al elegir esta opción, ej: "Simulacro completo (180 preguntas)".</summary>
        [Required(ErrorMessage = "La etiqueta es obligatoria")]
        [MaxLength(100)]
        [Display(Name = "Etiqueta")]
        public string Etiqueta { get; set; } = string.Empty;

        /// <summary>
        /// Duración en minutos. 0 = sin límite de tiempo.
        /// </summary>
        [Range(0, 600, ErrorMessage = "Debe ser entre 0 y 600 minutos (0 = sin tiempo)")]
        [Display(Name = "Duración (minutos)")]
        public int DuracionMinutos { get; set; }

        /// <summary>
        /// Número de preguntas para esta opción. 0 = usar el valor de TipoExamen.NumeroPreguntas.
        /// </summary>
        [Range(0, 500, ErrorMessage = "Debe ser entre 0 y 500")]
        [Display(Name = "N° de Preguntas")]
        public int NumeroPreguntas { get; set; } = 0;

        /// <summary>Orden de presentación en la lista de opciones del selector (menor = primero).</summary>
        public int Orden { get; set; } = 0;
    }
}
