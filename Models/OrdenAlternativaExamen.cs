namespace SimulacroExamen.Models
{
    /// <summary>
    /// Persiste el orden aleatorio de las alternativas dentro de una PreguntaExamen.
    /// Reemplaza el campo desnormalizado PreguntaExamen.OrdenAlternativas (IDs separados por coma).
    /// </summary>
    public class OrdenAlternativaExamen
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK al registro de PreguntaExamen al que pertenece este orden de alternativa.</summary>
        public int PreguntaExamenId { get; set; }

        /// <summary>Propiedad de navegación: la pregunta dentro del examen a la que corresponde este orden.</summary>
        public PreguntaExamen PreguntaExamen { get; set; } = null!;

        /// <summary>FK a la alternativa cuyo orden se está registrando para esta presentación.</summary>
        public int AlternativaId { get; set; }

        /// <summary>Propiedad de navegación: la alternativa con su texto y si es correcta.</summary>
        public Alternativa Alternativa { get; set; } = null!;

        /// <summary>Posición de esta alternativa (0 = primera).</summary>
        public int Orden { get; set; }
    }
}
