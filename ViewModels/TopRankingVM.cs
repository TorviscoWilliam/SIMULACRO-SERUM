namespace SimulacroExamen.ViewModels
{
    public class TopRankingVM
    {
        public string   NombreUsuario    { get; set; } = string.Empty;
        public string   TipoExamen       { get; set; } = string.Empty;
        public int      Puntaje          { get; set; }
        public int      TotalPreguntas   { get; set; }
        public double   PuntajeVigesimal { get; set; }
        public double   Porcentaje       { get; set; }
        public DateTime FechaFin         { get; set; }
    }
}
