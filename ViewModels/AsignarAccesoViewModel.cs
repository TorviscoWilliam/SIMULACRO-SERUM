namespace SimulacroExamen.ViewModels
{
    public class AsignarAccesoViewModel
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;

        /// <summary>Lista de todos los tipos de examen disponibles.</summary>
        public List<TipoAccesoItem> Tipos { get; set; } = new();
    }

    public class TipoAccesoItem
    {
        public int TipoExamenId { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>true si el usuario ya tiene acceso a este tipo.</summary>
        public bool Asignado { get; set; }
    }
}
