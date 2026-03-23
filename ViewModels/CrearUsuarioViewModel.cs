using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class CrearUsuarioViewModel
    {
        [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
        [MaxLength(100)]
        [Display(Name = "Nombre de Usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        [Display(Name = "Correo Electrónico")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme la contraseña")]
        [DataType(DataType.Password)]
        [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar Contraseña")]
        public string ConfirmarContrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "Seleccione un rol")]
        [Display(Name = "Rol")]
        public string Rol { get; set; } = "Usuario";

        [MaxLength(100)]
        [Display(Name = "Primer Nombre")]
        public string? PrimerNombre { get; set; }

        [MaxLength(100)]
        [Display(Name = "Segundo Nombre")]
        public string? SegundoNombre { get; set; }

        [MaxLength(100)]
        [Display(Name = "Primer Apellido")]
        public string? PrimerApellido { get; set; }

        [MaxLength(100)]
        [Display(Name = "Segundo Apellido")]
        public string? SegundoApellido { get; set; }

        [MaxLength(20)]
        [Display(Name = "Celular")]
        public string? Celular { get; set; }

        [MaxLength(20)]
        [Display(Name = "DNI")]
        public string? Dni { get; set; }

        [Display(Name = "Modo de prueba (Trial)")]
        public bool EsTrial { get; set; } = true;
    }
}
