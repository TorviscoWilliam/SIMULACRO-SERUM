using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class Sugerencia
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        [Required, MaxLength(100)]
        public string Asunto { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Mensaje { get; set; } = string.Empty;

        public DateTime FechaEnvio { get; set; } = DateTime.Now;

        /// <summary>false = nueva/no leída, true = revisada por admin</summary>
        public bool Leida { get; set; } = false;
    }
}
