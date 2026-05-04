using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Mensaje de sugerencia o comentario enviado por un usuario registrado al equipo administrador.
    /// Los administradores pueden revisar y marcar como leídas las sugerencias desde el panel de administración.
    /// </summary>
    public class Sugerencia
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK al usuario que envió la sugerencia.</summary>
        public int UsuarioId { get; set; }

        /// <summary>Propiedad de navegación: el usuario autor de la sugerencia.</summary>
        public Usuario? Usuario { get; set; }

        /// <summary>Resumen breve del tema de la sugerencia, ej: "Error en pregunta 42".</summary>
        [Required, MaxLength(100)]
        public string Asunto { get; set; } = string.Empty;

        /// <summary>Texto completo del mensaje enviado por el usuario.</summary>
        [Required, MaxLength(2000)]
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>Fecha y hora en que el usuario envió la sugerencia.</summary>
        public DateTime FechaEnvio { get; set; } = DateTime.Now;

        /// <summary>false = nueva/no leída, true = revisada por admin</summary>
        public bool Leida { get; set; } = false;
    }
}
