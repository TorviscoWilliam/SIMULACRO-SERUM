using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class ParametrosGlobales
    {
        public int Id { get; set; }

        [Range(1, 600)]
        public int TiempoMaximoSimulacroMinutos { get; set; } = 120;

        [Range(5, 480)]
        public int TiempoInactividadMinutos { get; set; } = 30;

        [Range(0, 100)]
        public int UmbralAprobacionPorcentaje { get; set; } = 70;

        [Range(0, 100)]
        public int UmbralRegularPorcentaje { get; set; } = 50;

        public DateTime FechaActualizacion { get; set; } = DateTime.Now;
        public int? AdminId { get; set; }
        public Usuario? Admin { get; set; }
    }
}
