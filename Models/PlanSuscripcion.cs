using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Plan de suscripción que se muestra en la página de precios del portal.
    /// Cada plan tiene un precio, una lista de características y un botón de acción
    /// que enlaza a WhatsApp o a una página de pago para que el usuario se suscriba.
    /// </summary>
    public class PlanSuscripcion
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>Nombre del plan, ej: "El Aplicado"</summary>
        [Required, MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Etiqueta debajo del nombre, ej: "PLAN MENSUAL"</summary>
        [MaxLength(100)]
        public string Etiqueta { get; set; } = "PLAN MENSUAL";

        /// <summary>Precio numérico, ej: 15</summary>
        [Range(0, 9999)]
        public decimal Precio { get; set; }

        /// <summary>Texto junto al precio, ej: "mensuales" o "por 2 meses"</summary>
        [MaxLength(50)]
        public string TextoPrecio { get; set; } = "mensuales";

        /// <summary>Color CSS inicial del degradado, ej: #74c0fc</summary>
        [MaxLength(20)]
        public string ColorPrimario { get; set; } = "#74c0fc";

        /// <summary>Color CSS final del degradado, ej: #4dabf7</summary>
        [MaxLength(20)]
        public string ColorSecundario { get; set; } = "#4dabf7";

        /// <summary>Muestra badge "Más popular" en rojo</summary>
        public bool EsPopular { get; set; } = false;

        /// <summary>Muestra un badge de alerta especial (ej: "Solo 50 primeros")</summary>
        [MaxLength(200)]
        public string? TextoBadge { get; set; }

        /// <summary>Lista de características del plan (una por fila en la tabla CaracteristicaPlan).</summary>
        public ICollection<CaracteristicaPlan> Caracteristicas { get; set; } = new List<CaracteristicaPlan>();

        /// <summary>Enlace de WhatsApp o página de pago</summary>
        [MaxLength(500)]
        public string EnlaceBoton { get; set; } = "https://wa.me/51936037152";

        /// <summary>Texto del botón de acción</summary>
        [MaxLength(80)]
        public string TextoBoton { get; set; } = "¡Suscribirme ya!";

        /// <summary>Controla si el plan es visible en la página de precios. false = oculto sin eliminar.</summary>
        public bool Activo { get; set; } = true;

        /// <summary>Orden de aparición (menor = primero)</summary>
        public int Orden { get; set; } = 0;

        /// <summary>Fecha y hora en que el plan fue creado por un administrador.</summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
