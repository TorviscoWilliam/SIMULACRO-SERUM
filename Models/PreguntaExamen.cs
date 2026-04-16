namespace SimulacroExamen.Models
{
    /// <summary>
    /// Tabla puente entre Examen y Pregunta. Registra el detalle de cada pregunta
    /// dentro de un examen específico: el orden en que apareció y qué alternativa
    /// eligió el usuario.
    ///
    /// El orden aleatorio de las alternativas se persiste en OrdenAlternativasExamen
    /// para que si el usuario recarga la página, las opciones aparezcan en el mismo orden.
    /// Si la respuesta fue correcta se deriva de AlternativaSeleccionada.EsCorrecta.
    /// </summary>
    public class PreguntaExamen
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK al examen al que pertenece este registro.</summary>
        public int ExamenId { get; set; }

        /// <summary>Propiedad de navegación: el examen dueño.</summary>
        public Examen Examen { get; set; } = null!;

        /// <summary>FK a la pregunta incluida en este examen.</summary>
        public int PreguntaId { get; set; }

        /// <summary>Propiedad de navegación: la pregunta con sus alternativas.</summary>
        public Pregunta Pregunta { get; set; } = null!;

        /// <summary>
        /// FK a la alternativa que seleccionó el usuario. Null si no respondió.
        /// Si la respuesta fue correcta se determina por AlternativaSeleccionada.EsCorrecta.
        /// </summary>
        public int? AlternativaSeleccionadaId { get; set; }

        /// <summary>Propiedad de navegación: la alternativa seleccionada (puede ser null).</summary>
        public Alternativa? AlternativaSeleccionada { get; set; }

        /// <summary>
        /// Posición de esta pregunta dentro del examen (1, 2, 3...).
        /// Se asigna aleatoriamente en IniciarExamen().
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Orden aleatorio de las alternativas para esta pregunta en este examen.
        /// Reemplaza el campo desnormalizado OrdenAlternativas (string CSV).
        /// </summary>
        public ICollection<OrdenAlternativaExamen> OrdenAlternativasExamen { get; set; } = new List<OrdenAlternativaExamen>();
    }
}
