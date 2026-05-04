using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Configuración SMTP del sistema. Se guarda una sola fila (Id = 1).
    /// Si no existe, EmailService usa los valores de appsettings.json como fallback.
    /// </summary>
    public class ConfiguracionCorreo
    {
        /// <summary>Clave primaria. Solo existe una fila con Id = 1 en la base de datos.</summary>
        public int Id { get; set; }

        /// <summary>Dirección del servidor SMTP, ej: smtp.gmail.com.</summary>
        [Required, MaxLength(200)]
        [Display(Name = "Servidor SMTP")]
        public string Smtp { get; set; } = "smtp.gmail.com";

        /// <summary>Puerto del servidor SMTP, típicamente 587 (TLS) o 465 (SSL).</summary>
        [Range(1, 65535)]
        [Display(Name = "Puerto")]
        public int Puerto { get; set; } = 587;

        /// <summary>Dirección de correo desde la que se envían los mensajes del sistema.</summary>
        [Required, MaxLength(200)]
        [EmailAddress]
        [Display(Name = "Correo remitente")]
        public string UsuarioCorreo { get; set; } = string.Empty;

        /// <summary>Contraseña de aplicación (App Password). Se almacena en texto plano igual que appsettings.</summary>
        [Required, MaxLength(200)]
        [Display(Name = "Contraseña / App Password")]
        public string Contrasena { get; set; } = string.Empty;

        /// <summary>Nombre visible del remitente que verá el destinatario en su cliente de correo, ej: "Simulacro SERUMS".</summary>
        [Required, MaxLength(200)]
        [Display(Name = "Nombre del remitente")]
        public string NombreRemitente { get; set; } = "Simulacro SERUMS";

        /// <summary>Habilita SSL/TLS en el cliente SMTP.</summary>
        [Display(Name = "Usar SSL")]
        public bool UsarSsl { get; set; } = true;

        /// <summary>Fecha y hora en que se guardó o modificó por última vez esta configuración.</summary>
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        /// <summary>FK al administrador que realizó el último cambio en la configuración de correo.</summary>
        public int AdminId { get; set; }
    }
}
