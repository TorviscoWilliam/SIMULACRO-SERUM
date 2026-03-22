using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class CrearNoticiaViewModel
    {
        [Required(ErrorMessage = "El título es obligatorio")]
        [MaxLength(200)]
        [Display(Name = "Título")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El contenido es obligatorio")]
        [Display(Name = "Contenido")]
        public string Contenido { get; set; } = string.Empty;

        [Display(Name = "Imagen (opcional)")]
        public IFormFile? Imagen { get; set; }

        [Display(Name = "Enlace (opcional)")]
        [MaxLength(1000)]
        [Url(ErrorMessage = "Ingresa una URL válida (ej. https://ejemplo.com)")]
        public string? EnlaceUrl { get; set; }
    }

    public class EditarNoticiaViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El título es obligatorio")]
        [MaxLength(200)]
        [Display(Name = "Título")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El contenido es obligatorio")]
        [Display(Name = "Contenido")]
        public string Contenido { get; set; } = string.Empty;

        [Display(Name = "Nueva imagen (opcional)")]
        public IFormFile? Imagen { get; set; }

        public string? ImagenRutaActual { get; set; }

        [Display(Name = "Enlace (opcional)")]
        [MaxLength(1000)]
        [Url(ErrorMessage = "Ingresa una URL válida (ej. https://ejemplo.com)")]
        public string? EnlaceUrl { get; set; }
    }

    public class NoticiaListaVM
    {
        public int      Id               { get; set; }
        public string   Titulo           { get; set; } = string.Empty;
        public string   Contenido        { get; set; } = string.Empty;
        public string?  ImagenRuta       { get; set; }
        public string?  EnlaceUrl        { get; set; }
        public DateTime FechaPublicacion { get; set; }
        public string   AdminNombre      { get; set; } = string.Empty;
        public bool     Activo           { get; set; }
    }
}
