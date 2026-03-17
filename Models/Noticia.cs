using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class Noticia
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El título es obligatorio")]
        [MaxLength(200)]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El contenido es obligatorio")]
        public string Contenido { get; set; } = string.Empty;

        /// <summary>Ruta relativa desde wwwroot (ej. /uploads/noticias/foto.jpg). Null si no tiene imagen.</summary>
        [MaxLength(500)]
        public string? ImagenRuta { get; set; }

        public DateTime FechaPublicacion { get; set; } = DateTime.Now;

        public int AdminId { get; set; }
        public Usuario? Admin { get; set; }

        public bool Activo { get; set; } = true;
    }
}
