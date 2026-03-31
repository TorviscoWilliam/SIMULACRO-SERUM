using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class AnuncioGlobal
    {
        public int Id { get; set; }

        [Required, MaxLength(500)]
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>info | warning | danger | success</summary>
        [MaxLength(20)]
        public string Tipo { get; set; } = "warning";

        public bool Activo { get; set; } = false;

        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        public int AdminId { get; set; }
        public Usuario? Admin { get; set; }
    }
}
