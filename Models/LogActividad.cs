using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class LogActividad
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        public int AdminId { get; set; }

        [MaxLength(100)]
        public string AdminNombre { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Accion { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Descripcion { get; set; } = string.Empty;
    }
}
