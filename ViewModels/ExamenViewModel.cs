namespace SimulacroExamen.ViewModels
{
    public class ExamenViewModel
    {
        public int ExamenId { get; set; }
        public List<PreguntaExamenVM> Preguntas { get; set; } = new();
    }

    public class PreguntaExamenVM
    {
        public int PreguntaExamenId { get; set; }
        public int PreguntaId       { get; set; }
        public int Orden            { get; set; }
        public string TextoPregunta { get; set; } = string.Empty;

        // Alternativas en el orden aleatorio para este examen
        public List<AlternativaVM> Alternativas { get; set; } = new();
    }

    public class AlternativaVM
    {
        public int    Id                { get; set; }
        public string TextoAlternativa  { get; set; } = string.Empty;
    }
}
