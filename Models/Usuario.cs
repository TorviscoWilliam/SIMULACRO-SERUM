using System.ComponentModel.DataAnnotations;

namespace SimulacroExamen.Models
{
    /// <summary>
    /// Clase base abstracta para todos los usuarios del sistema.
    /// Contiene los campos compartidos por administradores y estudiantes.
    /// EF Core usa herencia TPH (Table Per Hierarchy): una sola tabla "Usuarios"
    /// con columna discriminadora "Discriminador" que almacena el tipo concreto.
    ///
    /// Jerarquía:
    ///   Usuario (abstracta)
    ///   ├── Administrador  — Rol "Admin" o "SuperAdmin"
    ///   └── Estudiante     — Rol "Usuario"; agrega campos de suscripción y exámenes
    /// </summary>
    public abstract class Usuario
    {
        /// <summary>Clave primaria generada por SQL Server (IDENTITY).</summary>
        public int Id { get; set; }

        /// <summary>
        /// Nombre de inicio de sesión único en toda la tabla.
        /// Siempre en MAYÚSCULAS. Formato auto-generado: NOMBRE.APELLIDO.
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
        /// Rol del usuario: "Admin" | "SuperAdmin" | "Usuario".
        /// Controla el acceso mediante [Authorize(Roles="...")] y los claims de sesión.
        /// Distinto del discriminador de EF Core (que es "Administrador"/"Estudiante").
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Rol { get; set; } = string.Empty;

        /// <summary>Fecha y hora de creación de la cuenta.</summary>
        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Indica si la cuenta está habilitada.
        /// Un usuario inactivo no puede iniciar sesión (soft delete lógico).
        /// </summary>
        public bool Activo { get; set; } = true;

        // ── Datos personales ─────────────────────────────────────────
        [MaxLength(100)] public string? PrimerNombre    { get; set; }
        [MaxLength(100)] public string? SegundoNombre   { get; set; }
        [MaxLength(100)] public string? PrimerApellido  { get; set; }
        [MaxLength(100)] public string? SegundoApellido { get; set; }
        [MaxLength(20)]  public string? Celular         { get; set; }
        [MaxLength(8)]   public string? Dni             { get; set; }

        // ── Seguridad de sesión ───────────────────────────────────────
        /// <summary>Token hex-64 de la sesión activa. Se renueva en cada login.</summary>
        [MaxLength(128)] public string? SessionToken { get; set; }

        // ── Anti brute-force por usuario ──────────────────────────────
        /// <summary>Contador de intentos fallidos consecutivos. Se resetea al aplicar baneo.</summary>
        public int IntentosFallidos { get; set; } = 0;
        /// <summary>Fecha hasta la cual el usuario está bloqueado. Si es mayor a DateTime.Now, no puede iniciar sesión.</summary>
        public DateTime? FechaBaneo { get; set; }

        // ── Reset de contraseña ───────────────────────────────────────
        /// <summary>Token único para restablecer contraseña (expira en 1 hora).</summary>
        [MaxLength(100)]
        public string? PasswordResetToken { get; set; }

        /// <summary>Fecha/hora en que expira el token de reset.</summary>
        public DateTime? PasswordResetExpiry { get; set; }

        // ── Verificación de email ─────────────────────────────────────
        /// <summary>Indica si el usuario verificó su correo electrónico.</summary>
        public bool EmailVerificado { get; set; } = false;

        /// <summary>Token enviado al correo para verificar la cuenta.</summary>
        [MaxLength(100)]
        public string? EmailVerificacionToken { get; set; }

        // ── Propiedad calculada ───────────────────────────────────────
        public string NombreCompleto =>
            string.Join(" ", new[] { PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido }
                .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

        // ── Navegación inversa EF Core ────────────────────────────────
        /// <summary>Exámenes realizados por este usuario.</summary>
        public ICollection<Examen> Examenes { get; set; } = new List<Examen>();

        /// <summary>Tipos de examen asignados a este usuario.</summary>
        public ICollection<UsuarioTipoExamen> UsuariosTipoExamen { get; set; } = new List<UsuarioTipoExamen>();
    }
}
