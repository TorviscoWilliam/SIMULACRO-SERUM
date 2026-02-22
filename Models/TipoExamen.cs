using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    public class TipoExamen
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Tipo de Examen")]
        public string Nombre { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;

        public ICollection<Pregunta> Preguntas { get; set; } = new List<Pregunta>();
        public ICollection<Examen> Examenes { get; set; } = new List<Examen>();
        public ICollection<UsuarioTipoExamen> UsuariosTipoExamen { get; set; } = new List<UsuarioTipoExamen>();
    }
}
