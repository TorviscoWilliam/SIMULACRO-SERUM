using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel del formulario de configuración SMTP del sistema.
    /// Permite al administrador actualizar el servidor de correo desde la interfaz sin
    /// modificar appsettings.json. La contraseña nunca se devuelve al cliente en el GET.
    /// </summary>
    public class ConfiguracionCorreoViewModel
    {
        /// <summary>Dirección del servidor SMTP, ej: smtp.gmail.com.</summary>
        [Required(ErrorMessage = "El servidor SMTP es obligatorio")]
        [MaxLength(200)]
        [Display(Name = "Servidor SMTP")]
        public string Smtp { get; set; } = "smtp.gmail.com";

        /// <summary>Puerto del servidor SMTP, típicamente 587 (TLS) o 465 (SSL).</summary>
        [Range(1, 65535, ErrorMessage = "Puerto inválido")]
        [Display(Name = "Puerto")]
        public int Puerto { get; set; } = 587;

        /// <summary>Dirección de correo desde la que se envían los mensajes del sistema.</summary>
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

        /// <summary>Nombre visible del remitente que verá el destinatario en su cliente de correo.</summary>
        [Required(ErrorMessage = "El nombre del remitente es obligatorio")]
        [MaxLength(200)]
        [Display(Name = "Nombre del remitente")]
        public string NombreRemitente { get; set; } = "Simulacro SERUMS";

        /// <summary>Habilita SSL/TLS en el cliente SMTP.</summary>
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
