namespace SimulacroExamen.ViewModels
{
    public class PanelFinancieroViewModel
    {
        public List<MesFinancieroVM> Meses { get; set; } = new();
        public List<DistribucionPlanVM> DistribucionPlanes { get; set; } = new();

        public int TotalRegistrosPeriodo => Meses.Sum(m => m.RegistrosNuevos);
        public int TotalUsuariosPagoPeriodo => Meses.Sum(m => m.UsuariosPago);
        public decimal IngresosPeriodo => Meses.Sum(m => m.Ingresos);

        public double TasaConversionPeriodo => TotalRegistrosPeriodo > 0
            ? Math.Round((double)TotalUsuariosPagoPeriodo / TotalRegistrosPeriodo * 100, 1)
            : 0;

        public double PromedioRegistrosPorSemana { get; set; }

        public string[] Labels => Meses.Select(m => m.EtiquetaCorta).ToArray();
        public decimal[] IngresosMensuales => Meses.Select(m => m.Ingresos).ToArray();
        public int[] RegistrosMensuales => Meses.Select(m => m.RegistrosNuevos).ToArray();
        public double[] ConversionMensual => Meses.Select(m => m.TasaConversion).ToArray();
    }

    public class MesFinancieroVM
    {
        public int Mes { get; set; }
        public int Anio { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string EtiquetaCorta { get; set; } = string.Empty;
        public int RegistrosNuevos { get; set; }
        public int UsuariosTrial { get; set; }
        public int UsuariosPago { get; set; }
        public decimal Ingresos { get; set; }

        public double TasaConversion => RegistrosNuevos > 0
            ? Math.Round((double)UsuariosPago / RegistrosNuevos * 100, 1)
            : 0;
    }

    public class DistribucionPlanVM
    {
        public string NombrePlan { get; set; } = string.Empty;
        public int Usuarios { get; set; }
        public decimal IngresosEstimados { get; set; }
        public double Porcentaje { get; set; }
    }
}
