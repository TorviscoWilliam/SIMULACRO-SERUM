using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Característica individual de un PlanSuscripcion.
    /// Reemplaza el campo desnormalizado PlanSuscripcion.Caracteristicas (texto separado por \n).
    /// </summary>
    public class CaracteristicaPlan
    {
        public int Id { get; set; }

        public int PlanSuscripcionId { get; set; }
        public PlanSuscripcion Plan { get; set; } = null!;

        [Required, MaxLength(300)]
        public string Texto { get; set; } = string.Empty;

        /// <summary>Orden de aparición (0 = primero).</summary>
        public int Orden { get; set; } = 0;
    }
}
