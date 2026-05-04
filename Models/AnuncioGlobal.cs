using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Anuncio de alerta global que se muestra en la parte superior de todas las páginas.
    /// Solo puede haber un anuncio activo a la vez (Activo = true).
    /// El administrador puede crear, editar y activar/desactivar anuncios desde el panel de administración.
    /// </summary>
    public class AnuncioGlobal
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>Texto del anuncio que se mostrará al usuario en el banner de alerta.</summary>
        [Required, MaxLength(500)]
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>Tipo visual del alert de Bootstrap: info | warning | danger | success</summary>
        [MaxLength(20)]
        public string Tipo { get; set; } = "warning";

        /// <summary>Indica si el anuncio está visible actualmente en el sitio. Solo un anuncio debería estar activo al mismo tiempo.</summary>
        public bool Activo { get; set; } = false;

        /// <summary>Fecha y hora de la última modificación del anuncio, usada para mostrar antigüedad al admin.</summary>
        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        /// <summary>FK al administrador que creó o editó por última vez este anuncio.</summary>
        public int AdminId { get; set; }

        /// <summary>Propiedad de navegación: el administrador responsable del anuncio.</summary>
        public Usuario? Admin { get; set; }
    }
}
