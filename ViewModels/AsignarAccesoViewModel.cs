namespace SimulacroExamen.ViewModels
{
    /// <summary>
    /// ViewModel de la pantalla de asignación de acceso a tipos de examen para un usuario específico.
    /// Muestra todos los tipos disponibles con un indicador de cuáles ya tiene habilitados el usuario,
    /// permitiendo al administrador marcar o desmarcar cada uno con checkboxes.
    /// </summary>
    public class AsignarAccesoViewModel
    {
        /// <summary>ID del usuario al que se le está gestionando el acceso.</summary>
        public int UsuarioId { get; set; }

        /// <summary>Nombre de usuario mostrado en el encabezado de la pantalla de asignación.</summary>
        public string NombreUsuario { get; set; } = string.Empty;

        /// <summary>Lista de todos los tipos de examen disponibles.</summary>
        public List<TipoAccesoItem> Tipos { get; set; } = new();
    }

    /// <summary>
    /// Elemento individual de la lista de tipos de examen en la pantalla de asignación de acceso.
    /// Representa un tipo con su estado actual de asignación para el usuario en cuestión.
    /// </summary>
    public class TipoAccesoItem
    {
        /// <summary>ID del tipo de examen, usado como valor del checkbox al enviar el formulario.</summary>
        public int TipoExamenId { get; set; }

        /// <summary>Nombre del tipo de examen que se muestra como etiqueta del checkbox.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>true si el usuario ya tiene acceso a este tipo.</summary>
        public bool Asignado { get; set; }
    }
}
