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
    }

    public class NoticiaListaVM
    {
        public int      Id               { get; set; }
        public string   Titulo           { get; set; } = string.Empty;
        public string   Contenido        { get; set; } = string.Empty;
        public string?  ImagenRuta       { get; set; }
        public DateTime FechaPublicacion { get; set; }
        public string   AdminNombre      { get; set; } = string.Empty;
        public bool     Activo           { get; set; }
    }
}
