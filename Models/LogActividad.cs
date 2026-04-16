using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class LogActividad
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        public int AdminId { get; set; }

        /// <summary>Navegación al admin. Reemplaza el campo desnormalizado AdminNombre.</summary>
        public Usuario? Admin { get; set; }

        [MaxLength(100)]
        public string Accion { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Descripcion { get; set; } = string.Empty;
    }
}
