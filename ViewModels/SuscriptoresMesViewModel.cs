namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// Fila de la tabla de suscriptores: datos del estudiante + plan asignado.
    /// </summary>
    public class SuscriptorFilaVM
    {
        public int      Id                { get; set; }
        public string   NombreCompleto    { get; set; } = string.Empty;
        public string   NombreUsuario     { get; set; } = string.Empty;
        public string   Correo            { get; set; } = string.Empty;
        public string   Celular           { get; set; } = string.Empty;
        public string   NombrePlan        { get; set; } = string.Empty;
        public decimal  PrecioPlan        { get; set; }
        public DateTime FechaRegistro     { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public bool     Activo            { get; set; }
    }

    /// <summary>
    /// Modelo de la vista SuscriptoresMes: lista de estudiantes registrados
    /// en el mes seleccionado que ya tienen un plan de suscripción asignado,
    /// junto con los filtros activos y totales para el encabezado.
    /// </summary>
    public class SuscriptoresMesViewModel
    {
        public List<SuscriptorFilaVM> Suscriptores { get; set; } = new();

        /// <summary>Mes mostrado (1-12).</summary>
        public int Mes  { get; set; }

        /// <summary>Año mostrado.</summary>
        public int Anio { get; set; }

        /// <summary>Nombre del mes en español, ej: "Mayo 2026".</summary>
        public string MesNombre => new DateTime(Anio, Mes, 1).ToString("MMMM yyyy",
            System.Globalization.CultureInfo.GetCultureInfo("es-PE"));

        /// <summary>Total de suscriptores en el período.</summary>
        public int Total => Suscriptores.Count;

        /// <summary>Ingresos estimados: suma de precios de los planes asignados.</summary>
        public decimal IngresoEstimado => Suscriptores.Sum(s => s.PrecioPlan);

        /// <summary>Meses disponibles para el selector (últimos 12).</summary>
        public List<(int Mes, int Anio, string Etiqueta)> MesesDisponibles { get; set; } = new();
    }
}
