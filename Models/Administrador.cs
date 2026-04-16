namespace SimulacroExamen.Models
{
    /// <summary>
    /// Usuario con rol administrativo ("Admin" o "SuperAdmin").
    /// No agrega campos propios — toda la información está en <see cref="Usuario"/>.
    /// La distinción entre Admin y SuperAdmin se maneja a través de <see cref="Usuario.Rol"/>
    /// y los claims de autenticación, no mediante una subclase adicional.
    ///
    /// EF Core almacena estas filas con Discriminador = "Administrador" en la tabla Usuarios.
    /// </summary>
    public class Administrador : Usuario
    {
    }
}
