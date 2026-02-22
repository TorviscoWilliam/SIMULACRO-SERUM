namespace SimulacroExamen.Models
{
    public class UsuarioTipoExamen
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public int TipoExamenId { get; set; }
        public TipoExamen TipoExamen { get; set; } = null!;

        public DateTime FechaAsignacion { get; set; } = DateTime.Now;
    }
}
