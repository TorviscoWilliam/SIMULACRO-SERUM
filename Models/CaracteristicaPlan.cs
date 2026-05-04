using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Característica individual de un PlanSuscripcion.
    /// Reemplaza el campo desnormalizado PlanSuscripcion.Caracteristicas (texto separado por \n).
    /// </summary>
    public class CaracteristicaPlan
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK al plan de suscripción al que pertenece esta característica.</summary>
        public int PlanSuscripcionId { get; set; }

        /// <summary>Propiedad de navegación: el plan dueño de esta característica.</summary>
        public PlanSuscripcion Plan { get; set; } = null!;

        /// <summary>Descripción de la característica que se mostrará en la tarjeta del plan, ej: "180 preguntas SERUMS".</summary>
        [Required, MaxLength(300)]
        public string Texto { get; set; } = string.Empty;

        /// <summary>Orden de aparición en la lista de características del plan (0 = primero).</summary>
        public int Orden { get; set; } = 0;
    }
}
