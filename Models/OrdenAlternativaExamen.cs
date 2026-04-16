namespace SimulacroExamen.Models
{
    /// <summary>
    /// Persiste el orden aleatorio de las alternativas dentro de una PreguntaExamen.
    /// Reemplaza el campo desnormalizado PreguntaExamen.OrdenAlternativas (IDs separados por coma).
    /// </summary>
    public class OrdenAlternativaExamen
    {
        public int Id { get; set; }

        public int PreguntaExamenId { get; set; }
        public PreguntaExamen PreguntaExamen { get; set; } = null!;

        public int AlternativaId { get; set; }
        public Alternativa Alternativa { get; set; } = null!;

        /// <summary>Posición de esta alternativa (0 = primera).</summary>
        public int Orden { get; set; }
    }
}
