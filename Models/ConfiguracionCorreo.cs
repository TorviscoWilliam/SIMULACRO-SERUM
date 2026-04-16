using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Configuración SMTP del sistema. Se guarda una sola fila (Id = 1).
    /// Si no existe, EmailService usa los valores de appsettings.json como fallback.
    /// </summary>
    public class ConfiguracionCorreo
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Servidor SMTP")]
        public string Smtp { get; set; } = "smtp.gmail.com";

        [Range(1, 65535)]
        [Display(Name = "Puerto")]
        public int Puerto { get; set; } = 587;

        [Required, MaxLength(200)]
        [EmailAddress]
        [Display(Name = "Correo remitente")]
        public string UsuarioCorreo { get; set; } = string.Empty;

        /// <summary>Contraseña de aplicación (App Password). Se almacena en texto plano igual que appsettings.</summary>
        [Required, MaxLength(200)]
        [Display(Name = "Contraseña / App Password")]
        public string Contrasena { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        [Display(Name = "Nombre del remitente")]
        public string NombreRemitente { get; set; } = "Simulacro SERUMS";

        /// <summary>Habilita SSL/TLS en el cliente SMTP.</summary>
        [Display(Name = "Usar SSL")]
        public bool UsarSsl { get; set; } = true;

        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;
        public int AdminId { get; set; }
    }
}
