namespace SimulacroExamen.Models
{
    /// <summary>
    /// Tabla puente que controla el acceso de un usuario a un tipo de examen específico.
    /// Un usuario solo puede iniciar un examen de un tipo si existe un registro activo aquí.
    /// El administrador asigna o revoca el acceso desde el panel de gestión de usuarios.
    /// </summary>
    public class UsuarioTipoExamen
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>FK al usuario al que se le concede el acceso.</summary>
        public int UsuarioId { get; set; }

        /// <summary>Propiedad de navegación: el usuario beneficiado con el acceso.</summary>
        public Usuario Usuario { get; set; } = null!;

        /// <summary>FK al tipo de examen al que se otorga acceso.</summary>
        public int TipoExamenId { get; set; }

        /// <summary>Propiedad de navegación: el tipo de examen al que se da acceso.</summary>
        public TipoExamen TipoExamen { get; set; } = null!;

        /// <summary>Fecha y hora en que el administrador asignó el acceso a este usuario.</summary>
        public DateTime FechaAsignacion { get; set; } = DateTime.Now;
    }
}
