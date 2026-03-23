using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class RegisterViewModel
    {
        // ── Datos personales ─────────────────────────────────────────
        [Required(ErrorMessage = "El primer nombre es obligatorio")]
        [MaxLength(100)]
        [Display(Name = "Primer Nombre")]
        public string PrimerNombre { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Segundo Nombre")]
        public string? SegundoNombre { get; set; }

        [Required(ErrorMessage = "El primer apellido es obligatorio")]
        [MaxLength(100)]
        [Display(Name = "Primer Apellido")]
        public string PrimerApellido { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Segundo Apellido")]
        public string? SegundoApellido { get; set; }

        [Required(ErrorMessage = "El celular es obligatorio")]
        [MaxLength(20)]
        [Phone(ErrorMessage = "Formato de celular inválido")]
        [Display(Name = "Celular")]
        public string Celular { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio")]
        [StringLength(8, MinimumLength = 8, ErrorMessage = "El DNI debe tener exactamente 8 dígitos")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe contener solo 8 dígitos numéricos")]
        [Display(Name = "DNI")]
        public string Dni { get; set; } = string.Empty;

        // ── Credenciales ─────────────────────────────────────────────
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

        // ── Tipo de examen de prueba ─────────────────────────────────
        [Required(ErrorMessage = "Selecciona el tipo de examen que deseas probar")]
        [Display(Name = "Tipo de Examen")]
        public int TipoExamenId { get; set; }
    }
}
