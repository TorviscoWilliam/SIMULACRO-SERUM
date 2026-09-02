using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.ViewModels
{
    public class ParametrosGlobalesViewModel
    {
        [Display(Name = "Tiempo máximo del simulacro (min)")]
        [Range(1, 600, ErrorMessage = "Debe estar entre 1 y 600 minutos.")]
        public int TiempoMaximoSimulacroMinutos { get; set; } = 120;

        [Display(Name = "Tiempo de inactividad (min)")]
        [Range(5, 480, ErrorMessage = "Debe estar entre 5 y 480 minutos.")]
        public int TiempoInactividadMinutos { get; set; } = 30;

        [Display(Name = "Umbral de aprobación (%)")]
        [Range(0, 100, ErrorMessage = "Debe estar entre 0 y 100.")]
        public int UmbralAprobacionPorcentaje { get; set; } = 70;

        [Display(Name = "Umbral regular (%)")]
        [Range(0, 100, ErrorMessage = "Debe estar entre 0 y 100.")]
        public int UmbralRegularPorcentaje { get; set; } = 50;

        public DateTime? FechaActualizacion { get; set; }
        public string? AdminNombre { get; set; }
    }
}
