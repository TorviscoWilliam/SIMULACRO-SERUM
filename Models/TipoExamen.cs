using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Categoría de examen que agrupa un conjunto de preguntas del banco, ej: "SERUMS 2024".
    /// Define cuántas preguntas se incluyen por examen y el tiempo límite predeterminado.
    /// El acceso de cada usuario a un tipo de examen se controla mediante UsuarioTipoExamen.
    /// </summary>
    public class TipoExamen
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>Nombre identificador del tipo de examen mostrado en la interfaz, ej: "SERUMS 2024".</summary>
        [Required]
        [MaxLength(100)]
        [Display(Name = "Tipo de Examen")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Controla si este tipo de examen está habilitado para que los usuarios lo inicien. false = oculto sin eliminar.</summary>
        public bool Activo { get; set; } = true;

        /// <summary>Cantidad de preguntas que se seleccionan aleatoriamente del banco al iniciar un examen de este tipo.</summary>
        [Range(1, 200, ErrorMessage = "Debe ser entre 1 y 200")]
        [Display(Name = "N° de Preguntas por Examen")]
        public int NumeroPreguntas { get; set; } = 4;

        /// <summary>
        /// Duración en minutos para este tipo de examen.
        /// Si es null, se calcula automáticamente según el número de preguntas.
        /// </summary>
        [Range(1, 600, ErrorMessage = "Debe ser entre 1 y 600 minutos")]
        [Display(Name = "Duración (minutos)")]
        public int? DuracionMinutos { get; set; }

        /// <summary>Colección de preguntas del banco asociadas a este tipo de examen.</summary>
        public ICollection<Pregunta> Preguntas { get; set; } = new List<Pregunta>();

        /// <summary>Historial de todos los exámenes realizados bajo este tipo.</summary>
        public ICollection<Examen> Examenes { get; set; } = new List<Examen>();

        /// <summary>Registros de los usuarios que tienen acceso habilitado a este tipo de examen.</summary>
        public ICollection<UsuarioTipoExamen> UsuariosTipoExamen { get; set; } = new List<UsuarioTipoExamen>();

        /// <summary>Opciones de duración y número de preguntas que el usuario puede elegir al iniciar el examen.</summary>
        public ICollection<OpcionDuracion> OpcionesDuracion { get; set; } = new List<OpcionDuracion>();
    }
}
