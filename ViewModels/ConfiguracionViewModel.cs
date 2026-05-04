using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel del formulario de configuración personal del usuario autenticado.
    /// Permite cambiar la contraseña y establecer la nota ponderada que se usa para
    /// calcular la nota final ponderada al terminar un examen.
    /// </summary>
    public class ConfiguracionViewModel
    {
        // ── Cambio de contraseña ────────────────────────────────────
        /// <summary>Contraseña actual del usuario, requerida para autorizar el cambio de contraseña.</summary>
        [Display(Name = "Contraseña actual")]
        [DataType(DataType.Password)]
        public string? ContrasenaActual { get; set; }

        /// <summary>Nueva contraseña que el usuario desea establecer.</summary>
        [Display(Name = "Nueva contraseña")]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        [DataType(DataType.Password)]
        public string? ContrasenaNueva { get; set; }

        /// <summary>Confirmación de la nueva contraseña; debe coincidir con ContrasenaNueva.</summary>
        [Display(Name = "Confirmar nueva contraseña")]
        [DataType(DataType.Password)]
        [Compare("ContrasenaNueva", ErrorMessage = "Las contraseñas no coinciden")]
        public string? ConfirmarContrasena { get; set; }

        // ── Nota ponderada ──────────────────────────────────────────
        /// <summary>
        /// Nota del examen ponderado del usuario (escala 0–20).
        /// Se usa junto al puntaje del simulacro para calcular la nota final:
        /// NotaFinal = PuntajeVigesimal × 0.70 + NotaPonderada × 0.30.
        /// </summary>
        [Display(Name = "Nota ponderada (0 – 20)")]
        [Range(0, 20, ErrorMessage = "La nota debe estar entre 0 y 20")]
        public double? NotaPonderada { get; set; }
    }
}
