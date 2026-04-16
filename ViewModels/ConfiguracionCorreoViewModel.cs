using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class ConfiguracionCorreoViewModel
    {
        [Required(ErrorMessage = "El servidor SMTP es obligatorio")]
        [MaxLength(200)]
        [Display(Name = "Servidor SMTP")]
        public string Smtp { get; set; } = "smtp.gmail.com";

        [Range(1, 65535, ErrorMessage = "Puerto inválido")]
        [Display(Name = "Puerto")]
        public int Puerto { get; set; } = 587;

        [Required(ErrorMessage = "El correo remitente es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        [MaxLength(200)]
        [Display(Name = "Correo remitente")]
        public string UsuarioCorreo { get; set; } = string.Empty;

        /// <summary>
        /// Se deja vacío en el GET para no enviar la contraseña al cliente.
        /// Solo se guarda si el campo no está vacío en el POST.
        /// </summary>
        [MaxLength(200)]
        [Display(Name = "Contraseña / App Password")]
        public string? Contrasena { get; set; }

        [Required(ErrorMessage = "El nombre del remitente es obligatorio")]
        [MaxLength(200)]
        [Display(Name = "Nombre del remitente")]
        public string NombreRemitente { get; set; } = "Simulacro SERUMS";

        [Display(Name = "Usar SSL")]
        public bool UsarSsl { get; set; } = true;

        /// <summary>Indica si ya existe una configuración guardada en BD (para mostrar estado en la vista).</summary>
        public bool YaConfigurado { get; set; }

        /// <summary>Correo destino para el envío de prueba (no se persiste).</summary>
        [EmailAddress(ErrorMessage = "Correo de prueba inválido")]
        [Display(Name = "Enviar correo de prueba a")]
        public string? CorreoPrueba { get; set; }
    }
}
