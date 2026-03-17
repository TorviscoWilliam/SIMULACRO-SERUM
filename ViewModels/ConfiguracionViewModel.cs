using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class ConfiguracionViewModel
    {
        // ── Cambio de contraseña ────────────────────────────────────
        [Display(Name = "Contraseña actual")]
        [DataType(DataType.Password)]
        public string? ContrasenaActual { get; set; }

        [Display(Name = "Nueva contraseña")]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        [DataType(DataType.Password)]
        public string? ContrasenaNueva { get; set; }

        [Display(Name = "Confirmar nueva contraseña")]
        [DataType(DataType.Password)]
        [Compare("ContrasenaNueva", ErrorMessage = "Las contraseñas no coinciden")]
        public string? ConfirmarContrasena { get; set; }

        // ── Nota ponderada ──────────────────────────────────────────
        [Display(Name = "Nota ponderada (0 – 20)")]
        [Range(0, 20, ErrorMessage = "La nota debe estar entre 0 y 20")]
        public double? NotaPonderada { get; set; }
    }
}
