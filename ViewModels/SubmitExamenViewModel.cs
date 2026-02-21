namespace SimulacroExamen.ViewModels
{
    public class SubmitExamenViewModel
    {
        public int ExamenId { get; set; }

        // Clave: PreguntaExamenId  |  Valor: AlternativaId seleccionada
        public Dictionary<int, int> Respuestas { get; set; } = new();
    }
}
