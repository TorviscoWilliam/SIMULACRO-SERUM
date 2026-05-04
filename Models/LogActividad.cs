using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Registro de auditoría de las acciones realizadas por administradores en el sistema.
    /// Permite rastrear quién hizo qué y cuándo, facilitando la detección de errores o usos indebidos.
    /// </summary>
    public class LogActividad
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>Fecha y hora exacta en que se realizó la acción registrada.</summary>
        public DateTime Fecha { get; set; } = DateTime.Now;

        /// <summary>FK al administrador que ejecutó la acción.</summary>
        public int AdminId { get; set; }

        /// <summary>Navegación al admin. Reemplaza el campo desnormalizado AdminNombre.</summary>
        public Usuario? Admin { get; set; }

        /// <summary>Nombre corto de la acción ejecutada, ej: "Crear pregunta", "Eliminar usuario".</summary>
        [MaxLength(100)]
        public string Accion { get; set; } = string.Empty;

        /// <summary>Detalle adicional sobre la acción, ej: datos del registro afectado o contexto relevante.</summary>
        [MaxLength(500)]
        public string Descripcion { get; set; } = string.Empty;
    }
}
