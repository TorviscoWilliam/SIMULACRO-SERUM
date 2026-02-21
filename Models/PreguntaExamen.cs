namespace SimulacroExamen.Models
{
    public class PreguntaExamen
    {
        public int Id { get; set; }

        public int ExamenId { get; set; }
        public Examen Examen { get; set; } = null!;

        public int PreguntaId { get; set; }
        public Pregunta Pregunta { get; set; } = null!;

        // Alternativa elegida por el usuario (null si no respondió)
        public int? AlternativaSeleccionadaId { get; set; }
        public Alternativa? AlternativaSeleccionada { get; set; }

        public bool EsCorrecta { get; set; } = false;

        // Posición de la pregunta en el examen (orden aleatorio)
        public int Orden { get; set; } = 0;

        // IDs de alternativas separados por coma en orden aleatorio
        // Ej: "3,1,4,2"
        public string OrdenAlternativas { get; set; } = string.Empty;
    }
}
