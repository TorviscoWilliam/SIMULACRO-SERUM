namespace SimulacroExamen.Models
{
    /// <summary>
    /// Usuario que rinde exámenes en la plataforma (Rol = "Usuario").
    /// Extiende <see cref="Usuario"/> con campos exclusivos del flujo de examinación:
    /// nota ponderada personal, intentos extra diarios y estado de suscripción.
    ///
    /// EF Core almacena estas filas con Discriminador = "Estudiante" en la tabla Usuarios.
    /// </summary>
    public class Estudiante : Usuario
    {
        /// <summary>
        /// Nota ponderada en escala vigesimal (0–20), configurada por el propio estudiante.
        /// Se usa para calcular la nota final: PuntajeVigesimal × 0.70 + NotaPonderada × 0.30.
        /// Null si el estudiante no la ha configurado.
        /// </summary>
        public double? NotaPonderada { get; set; }

        /// <summary>
        /// Intentos de examen extra otorgados por un administrador.
        /// Límite diario efectivo = 5 + IntentosExtra.
        /// </summary>
        public int IntentosExtra { get; set; } = 0;

        /// <summary>
        /// Indica si el estudiante está en modo de prueba (trial).
        /// true = acceso limitado a 1 examen de 20 preguntas.
        /// false = acceso completo según los tipos y suscripción asignados.
        /// </summary>
        public bool EsTrial { get; set; } = true;

        /// <summary>
        /// Fecha de vencimiento de la suscripción.
        /// null = acceso sin fecha límite.
        /// Si es pasada y EsTrial = false → suscripción vencida, se bloquea el acceso.
        /// </summary>
        public DateTime? FechaVencimiento { get; set; }

        /// <summary>
        /// FK al plan de suscripción contratado.
        /// null = sin plan asignado (trial o acceso manual sin plan específico).
        /// Se asigna al activar acceso completo o al editar el usuario.
        /// </summary>
        public int? PlanSuscripcionId { get; set; }

        /// <summary>Navegación al plan contratado.</summary>
        public PlanSuscripcion? PlanSuscripcion { get; set; }
    }
}
