namespace SimulacroExamen.ViewModels
{
    public class EstadisticaPreguntaVM
    {
        public int?   PreguntaId    { get; set; }
        public string TextoPregunta { get; set; } = string.Empty;
        public string TipoNombre    { get; set; } = string.Empty;
        public int    TotalVeces    { get; set; }
        public int    Correctas     { get; set; }
        public int    Incorrectas   { get; set; }
        public double PorcentajeError => TotalVeces > 0 ? (double)Incorrectas / TotalVeces * 100 : 0;
    }
}
