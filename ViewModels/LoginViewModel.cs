using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel del formulario de inicio de sesión (GET/POST Account/Login).
    /// Contiene solo los datos que el usuario ingresa en el formulario;
    /// la autenticación real ocurre en AccountController.Login().
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>Nombre de usuario ingresado. Se busca en la tabla Usuarios con Activo=true.</summary>
        [Required(ErrorMessage = "El usuario es obligatorio")]
        [Display(Name = "Usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña en texto plano. Se compara contra el hash BCrypt almacenado en la BD.
        /// DataType.Password evita que el navegador la cachee y la oculta en el HTML.
        /// </summary>
        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; } = string.Empty;

        /// <summary>
        /// URL a la que redirigir tras el login exitoso.
        /// Viene del middleware de autorización cuando el usuario accede directamente
        /// a una ruta protegida sin estar autenticado. Validado con Url.IsLocalUrl()
        /// para prevenir ataques de open redirect.
        /// </summary>
        public string? ReturnUrl { get; set; }

        /// <summary>
        /// Cuando es false la cookie de sesión se borra al cerrar el navegador;
        /// útil en dispositivos compartidos. Cuando es true persiste el tiempo
        /// configurado (8h) aunque el usuario cierre el navegador.
        /// </summary>
        [Display(Name = "Mantener sesión iniciada")]
        public bool Recordarme { get; set; } = false;

        /// <summary>
        /// Token generado por reCAPTCHA v3 en el cliente y enviado en el campo
        /// oculto "g-recaptcha-response". Lo valida RecaptchaService contra
        /// la API de Google antes de tocar la BD.
        /// </summary>
        public string? RecaptchaToken { get; set; }
    }
}
