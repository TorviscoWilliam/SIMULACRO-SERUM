using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class EditarUsuarioViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
        [MaxLength(100)]
        [Display(Name = "Nombre de Usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        [Display(Name = "Correo Electrónico")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Seleccione un rol")]
        [Display(Name = "Rol")]
        public string Rol { get; set; } = "Usuario";

        [MaxLength(100)] [Display(Name = "Primer Nombre")]    public string? PrimerNombre    { get; set; }
        [MaxLength(100)] [Display(Name = "Segundo Nombre")]   public string? SegundoNombre   { get; set; }
        [MaxLength(100)] [Display(Name = "Primer Apellido")]  public string? PrimerApellido  { get; set; }
        [MaxLength(100)] [Display(Name = "Segundo Apellido")] public string? SegundoApellido { get; set; }
        [MaxLength(20)]  [Display(Name = "Celular")]           public string? Celular         { get; set; }

        [StringLength(8, MinimumLength = 8, ErrorMessage = "El DNI debe tener exactamente 8 dígitos")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe contener solo 8 dígitos numéricos")]
        [Display(Name = "DNI")]
        public string? Dni { get; set; }

        [Display(Name = "Fecha de Vencimiento")]
        public DateTime? FechaVencimiento { get; set; }

        [Display(Name = "Plan de Suscripción")]
        public int? PlanSuscripcionId { get; set; }

        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        [Display(Name = "Nueva Contraseña")]
        public string? ContrasenaNueva { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(ContrasenaNueva), ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar Nueva Contraseña")]
        public string? ConfirmarContrasena { get; set; }
    }
}
