namespace SimulacroExamen.ViewModels
{
    public class ResultadoExamenViewModel
    {
        public int      ExamenId        { get; set; }
        public int      Puntaje         { get; set; }
        public int      TotalPreguntas  { get; set; }
        public double   Porcentaje      { get; set; }
        public DateTime FechaInicio     { get; set; }
        public DateTime FechaFin        { get; set; }
        public TimeSpan Duracion        => FechaFin - FechaInicio;

        public List<DetalleRespuestaVM> Detalles { get; set; } = new();
    }

    public class DetalleRespuestaVM
    {
        public int     Numero               { get; set; }
        public string  TextoPregunta        { get; set; } = string.Empty;
        public string  RespuestaCorrecta    { get; set; } = string.Empty;
        public string? RespuestaSeleccionada { get; set; }
        public bool    EsCorrecta           { get; set; }
    }
}
