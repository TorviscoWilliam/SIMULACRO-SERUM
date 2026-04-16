namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel de la vista Admin/Usuarios y del Excel exportado.
    /// Proyectado directamente desde la BD con Select() para calcular TotalExamenes
    /// y MejorPuntaje en SQL (más eficiente que cargar todos los exámenes al cliente).
    /// También expone TiempoFormateado, calculado en el ViewModel a partir de FechaCreacion.
    /// </summary>
    public class UsuarioListaViewModel
    {
        /// <summary>ID del usuario (clave primaria). Usado en ToggleUsuario para identificar el registro.</summary>
        public int      Id                  { get; set; }

        /// <summary>Nombre de usuario único. Se muestra en la tabla y en el avatar circular.</summary>
        public string   NombreUsuario       { get; set; } = string.Empty;

        /// <summary>Correo electrónico del usuario.</summary>
        public string   Correo              { get; set; } = string.Empty;

        /// <summary>Rol del usuario ("Admin" o "Usuario"). Siempre "Usuario" en esta lista.</summary>
        public string   Rol                 { get; set; } = string.Empty;

        /// <summary>Fecha y hora en que se creó la cuenta. Base para calcular TiempoFormateado.</summary>
        public DateTime FechaCreacion       { get; set; }

        /// <summary>
        /// Estado de la cuenta. false = desactivado (no puede iniciar sesión).
        /// Se alterna con ToggleUsuario (soft delete lógico).
        /// </summary>
        public bool     Activo              { get; set; }

        /// <summary>
        /// Número de exámenes completados por este usuario.
        /// Calculado en SQL: u.Examenes.Count(e => e.Completado).
        /// </summary>
        public int      TotalExamenes       { get; set; }

        /// <summary>
        /// Mejor puntaje absoluto (no porcentaje) obtenido en cualquier examen completado.
        /// 0 si el usuario nunca ha completado un examen.
        /// </summary>
        public int      MejorPuntaje        { get; set; }

        /// <summary>Nombres de los tipos de examen asignados a este usuario.</summary>
        public List<string> TiposAsignados  { get; set; } = new();

        /// <summary>Intentos de examen extra otorgados por un admin (límite diario = 5 + IntentosExtra).</summary>
        public int IntentosExtra { get; set; }

        /// <summary>Nombre completo (PrimerNombre SegundoNombre PrimerApellido SegundoApellido). Null si no se llenó al registrarse.</summary>
        public string? NombreCompleto { get; set; }

        /// <summary>Celular registrado. Null si no se proporcionó.</summary>
        public string? Celular { get; set; }

        /// <summary>DNI de 8 dígitos. Null en cuentas creadas antes de este campo.</summary>
        public string? Dni { get; set; }

        /// <summary>true = usuario en modo trial (1 examen de prueba). false = acceso completo.</summary>
        public bool EsTrial { get; set; }

        /// <summary>Fecha en que vence la suscripción. null = sin vencimiento.</summary>
        public DateTime? FechaVencimiento { get; set; }

        /// <summary>ID del plan contratado. Null si no tiene plan asignado.</summary>
        public int? PlanSuscripcionId { get; set; }

        /// <summary>Nombre del plan contratado. Null si no tiene plan asignado.</summary>
        public string? PlanNombre { get; set; }

        /// <summary>true si la suscripción ya venció (no trial, tiene fecha, ya pasó).</summary>
        public bool SuscripcionVencida => !EsTrial && FechaVencimiento.HasValue && FechaVencimiento.Value < DateTime.Now;

        /// <summary>Días restantes de suscripción. 0 si no aplica o ya venció.</summary>
        public int DiasRestantes => (!EsTrial && FechaVencimiento.HasValue && FechaVencimiento.Value >= DateTime.Now)
            ? (int)Math.Ceiling((FechaVencimiento.Value - DateTime.Now).TotalDays) : 0;

        // ── Propiedades calculadas (no persistidas en BD) ──────────────

        /// <summary>
        /// Tiempo transcurrido desde que se creó la cuenta hasta ahora.
        /// Se recalcula en cada acceso (no se persiste). Base para TiempoFormateado.
        /// </summary>
        public TimeSpan TiempoDesdeCreacion => DateTime.Now - FechaCreacion;

        /// <summary>
        /// Versión legible del tiempo transcurrido desde la creación de la cuenta.
        /// Ejemplos: "2 año(s), 3 mes(es)" | "5 mes(es), 12 día(s)" | "3 día(s)" | "4 hora(s)" | "15 minuto(s)".
        /// Se usa en la vista Admin/Usuarios como badge y en la columna "Tiempo Registrado" del Excel.
        /// Algoritmo de cascada: muestra la unidad más significativa primero.
        /// </summary>
        public string TiempoFormateado
        {
            get
            {
                var t = TiempoDesdeCreacion;

                // Más de un año: mostrar años y meses restantes
                if (t.TotalDays >= 365)
                    return $"{(int)(t.TotalDays / 365)} año(s), {(int)(t.TotalDays % 365 / 30)} mes(es)";

                // Más de un mes: mostrar meses y días restantes
                if (t.TotalDays >= 30)
                    return $"{(int)(t.TotalDays / 30)} mes(es), {(int)(t.TotalDays % 30)} día(s)";

                // Más de un día
                if (t.TotalDays >= 1)
                    return $"{(int)t.TotalDays} día(s)";

                // Más de una hora
                if (t.TotalHours >= 1)
                    return $"{(int)t.TotalHours} hora(s)";

                // Menos de una hora: mostrar minutos
                return $"{(int)t.TotalMinutes} minuto(s)";
            }
        }
    }
}
