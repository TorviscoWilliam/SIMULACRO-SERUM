using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Artículo de noticias o comunicado publicado por un administrador en el portal.
    /// Las noticias pueden incluir imagen y un enlace externo, y pueden activarse o desactivarse
    /// sin necesidad de eliminarlas.
    /// </summary>
    public class Noticia
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>Título de la noticia que aparece como encabezado en la tarjeta o detalle.</summary>
        [Required(ErrorMessage = "El título es obligatorio")]
        [MaxLength(200)]
        public string Titulo { get; set; } = string.Empty;

        /// <summary>Cuerpo completo de la noticia. Puede contener HTML para dar formato.</summary>
        [Required(ErrorMessage = "El contenido es obligatorio")]
        public string Contenido { get; set; } = string.Empty;

        /// <summary>Ruta relativa desde wwwroot (ej. /uploads/noticias/foto.jpg). Null si no tiene imagen.</summary>
        [MaxLength(500)]
        public string? ImagenRuta { get; set; }

        /// <summary>URL externa opcional a la que se puede redirigir al usuario (ej. enlace de convocatoria).</summary>
        [MaxLength(1000)]
        public string? EnlaceUrl { get; set; }

        /// <summary>Fecha y hora en que la noticia fue publicada o creada por el administrador.</summary>
        public DateTime FechaPublicacion { get; set; } = DateTime.Now;

        /// <summary>FK al administrador que publicó la noticia.</summary>
        public int AdminId { get; set; }

        /// <summary>Propiedad de navegación: el administrador que creó la noticia.</summary>
        public Usuario? Admin { get; set; }

        /// <summary>Controla si la noticia es visible para los usuarios. false = oculta sin eliminar.</summary>
        public bool Activo { get; set; } = true;
    }
}
