namespace SimulacroExamen.Models
{
    /// <summary>
    /// Tabla puente entre Examen y Pregunta. Registra el detalle de cada pregunta
    /// dentro de un examen específico: el orden en que apareció, qué alternativa
    /// eligió el usuario y si la respuesta fue correcta.
    ///
    /// También persiste el orden aleatorio de las alternativas (OrdenAlternativas)
    /// para que si el usuario recarga la página durante el examen, las opciones
    /// aparezcan en el mismo orden y no se re-aleatoricen.
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
        /// Al calificar: se verifica AlternativaSeleccionada.EsCorrecta.
        /// Configurado con SetNull en EF Core para no afectar la alternativa original.
        /// </summary>
        public int? AlternativaSeleccionadaId { get; set; }

        /// <summary>Propiedad de navegación: la alternativa seleccionada (puede ser null).</summary>
        public Alternativa? AlternativaSeleccionada { get; set; }

        /// <summary>
        /// true si la alternativa seleccionada fue la correcta. Se establece en Enviar().
        /// Permite mostrar resultados históricos sin recalcular.
        /// </summary>
        public bool EsCorrecta { get; set; } = false;

        /// <summary>
        /// Posición de esta pregunta dentro del examen (1, 2, 3...).
        /// Se asigna aleatoriamente en IniciarExamen() y permite mostrar
        /// las preguntas siempre en el mismo orden aunque se recargue la página.
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// IDs de las alternativas en el orden aleatorio en que deben mostrarse,
        /// separados por coma. Ej: "3,1,4,2" (ID de alternativa por columna).
        /// Se genera en IniciarExamen() y se lee en Tomar() para reconstruir
        /// la vista sin re-aleatorizar. Máx. 500 caracteres (suficiente para 4 IDs).
        /// </summary>
        public string OrdenAlternativas { get; set; } = string.Empty;
    }
}
