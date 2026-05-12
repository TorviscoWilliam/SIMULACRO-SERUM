using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El usuario es obligatorio")]
        [MaxLength(15, ErrorMessage = "El usuario no puede superar 15 caracteres")]
        [Display(Name = "Usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [DataType(DataType.Password)]
        [MaxLength(50, ErrorMessage = "La contraseña no puede superar 50 caracteres")]
        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; } = string.Empty;

        /// <summary>
        /// URL a la que redirigir tras el login exitoso.
        /// Validado con Url.IsLocalUrl() en el controller para prevenir open redirect.
        /// </summary>
        public string? ReturnUrl { get; set; }

        [Display(Name = "Mantener sesión iniciada")]
        public bool Recordarme { get; set; } = false;

        public string? RecaptchaToken { get; set; }
    }
}
