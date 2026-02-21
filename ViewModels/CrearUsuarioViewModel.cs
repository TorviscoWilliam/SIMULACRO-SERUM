using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel del formulario Admin → Crear Usuario.
    /// Incluye validación de confirmación de contraseña que no existe en el modelo de BD.
    /// El controlador verifica además la unicidad de NombreUsuario y Correo antes de insertar.
    /// El rol siempre se asigna como "Usuario"; nunca se puede crear un Admin desde la UI.
    /// </summary>
    public class CrearUsuarioViewModel
    {
        /// <summary>
        /// Nombre de usuario único. MaxLength(100) limita el campo tanto en validación
        /// del lado cliente como del servidor. Se comprueba unicidad en AdminController.
        /// </summary>
        [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
        [MaxLength(100)]
        [Display(Name = "Nombre de Usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        /// <summary>
        /// Correo electrónico único del usuario. La anotación [EmailAddress] valida
        /// el formato (xxx@yyy.zzz) pero no verifica que la dirección exista.
        /// </summary>
        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        [Display(Name = "Correo Electrónico")]
        public string Correo { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña en texto plano. Solo existe en este ViewModel; en la BD se guarda
        /// el hash BCrypt. MinLength(6) exige un mínimo de seguridad básico.
        /// </summary>
        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; } = string.Empty;

        /// <summary>
        /// Confirmación de contraseña. [Compare] verifica en el servidor que coincida
        /// exactamente con Contrasena. Este campo no se persiste en la BD.
        /// </summary>
        [Required(ErrorMessage = "Confirme la contraseña")]
        [DataType(DataType.Password)]
        [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar Contraseña")]
        public string ConfirmarContrasena { get; set; } = string.Empty;
    }
}
