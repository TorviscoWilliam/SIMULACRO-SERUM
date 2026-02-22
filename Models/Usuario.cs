using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Representa a una persona registrada en el sistema.
    /// Puede tener el rol "Admin" (administrador) o "Usuario" (examinado).
    /// La contraseña se almacena como hash BCrypt; nunca en texto plano.
    /// </summary>
    public class Usuario
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>
        /// Nombre de inicio de sesión. Debe ser único en toda la tabla.
        /// Se usa junto con la contraseña para autenticar al usuario.
        /// </summary>
        [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
        [MaxLength(100)]
        [Display(Name = "Nombre de Usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        /// <summary>Dirección de correo electrónico. Único en toda la tabla.</summary>
        [Required(ErrorMessage = "El correo es obligatorio")]
        [MaxLength(200)]
        [EmailAddress(ErrorMessage = "Correo inválido")]
        [Display(Name = "Correo Electrónico")]
        public string Correo { get; set; } = string.Empty;

        /// <summary>
        /// Hash BCrypt de la contraseña (máx. 255 caracteres).
        /// Para verificar: BCrypt.Net.BCrypt.Verify(textoPlano, hash).
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Contrasena { get; set; } = string.Empty;

        /// <summary>
        /// Rol del usuario: "Admin" (administrador) | "Usuario" (examinado).
        /// Controla el acceso a los controladores mediante [Authorize(Roles="...")].
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Rol { get; set; } = "Usuario"; // Valor por defecto al crear desde el admin

        /// <summary>
        /// Fecha y hora en que el administrador creó la cuenta.
        /// Permite calcular el "tiempo desde creación" visible en la lista de usuarios.
        /// </summary>
        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Indica si la cuenta está habilitada.
        /// Un usuario inactivo no puede iniciar sesión.
        /// El admin puede activar/desactivar sin eliminar registros (soft delete lógico).
        /// </summary>
        public bool Activo { get; set; } = true;

        // ── Propiedades de navegación EF Core ───────────────────────
        /// <summary>Lista de todos los exámenes que ha realizado este usuario.</summary>
        public ICollection<Examen> Examenes { get; set; } = new List<Examen>();

        /// <summary>Tipos de examen a los que el admin le ha dado acceso a este usuario.</summary>
        public ICollection<UsuarioTipoExamen> UsuariosTipoExamen { get; set; } = new List<UsuarioTipoExamen>();
    }
}
