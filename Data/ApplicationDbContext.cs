using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Models;

namespace SimulacroExamen.Data
{
    /// <summary>
    /// Contexto de base de datos principal de la aplicación (EF Core).
    /// Extiende DbContext y expone las cinco tablas del sistema.
    /// La configuración de relaciones, restricciones e índices se define
    /// en OnModelCreating usando Fluent API (preferida sobre atributos
    /// para reglas complejas como claves foráneas con comportamientos específicos).
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Constructor que recibe la configuración de la conexión desde Program.cs
        /// mediante inyección de dependencias (DI).
        /// </summary>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // ── Tablas (DbSet = tabla en la BD) ─────────────────────────
        public DbSet<Usuario>        Usuarios        { get; set; }
        public DbSet<Pregunta>       Preguntas       { get; set; }
        public DbSet<Alternativa>    Alternativas    { get; set; }
        public DbSet<Examen>         Examenes        { get; set; }
        public DbSet<PreguntaExamen> PreguntasExamen { get; set; }

        /// <summary>
        /// Configura el modelo de datos usando Fluent API.
        /// Se ejecuta una sola vez al crear el contexto.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Usuarios ──────────────────────────────────────────────
            modelBuilder.Entity<Usuario>(e =>
            {
                // Restricciones de unicidad a nivel de BD (el código ya valida antes de insertar)
                e.HasIndex(u => u.NombreUsuario).IsUnique();
                e.HasIndex(u => u.Correo).IsUnique();
                e.Property(u => u.Contrasena).HasMaxLength(255);
                // Valor por defecto a nivel de BD si se inserta con SQL directo
                e.Property(u => u.FechaCreacion).HasDefaultValueSql("GETDATE()");
            });

            // ── Preguntas ─────────────────────────────────────────────
            modelBuilder.Entity<Pregunta>(e =>
            {
                e.Property(p => p.FechaCreacion).HasDefaultValueSql("GETDATE()");
                // nvarchar(max) permite preguntas de cualquier longitud
                e.Property(p => p.TextoPregunta).HasColumnType("nvarchar(max)");
            });

            // ── Alternativas ──────────────────────────────────────────
            modelBuilder.Entity<Alternativa>(e =>
            {
                // Cascade: al eliminar una Pregunta, sus Alternativas se borran automáticamente
                e.HasOne(a => a.Pregunta)
                 .WithMany(p => p.Alternativas)
                 .HasForeignKey(a => a.PreguntaId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(a => a.TextoAlternativa).HasColumnType("nvarchar(max)");
            });

            // ── Examenes ──────────────────────────────────────────────
            modelBuilder.Entity<Examen>(e =>
            {
                // Restrict: no se puede borrar un Usuario si tiene exámenes
                // (protege el historial; se usa soft delete en lugar de borrado físico)
                e.HasOne(ex => ex.Usuario)
                 .WithMany(u => u.Examenes)
                 .HasForeignKey(ex => ex.UsuarioId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(ex => ex.FechaInicio).HasDefaultValueSql("GETDATE()");
            });

            // ── PreguntasExamen ───────────────────────────────────────
            modelBuilder.Entity<PreguntaExamen>(e =>
            {
                // Cascade: al eliminar un Examen, sus PreguntasExamen se eliminan también
                e.HasOne(pe => pe.Examen)
                 .WithMany(ex => ex.PreguntasExamen)
                 .HasForeignKey(pe => pe.ExamenId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Restrict: no eliminar una Pregunta si fue usada en algún examen
                e.HasOne(pe => pe.Pregunta)
                 .WithMany(p => p.PreguntasExamen)
                 .HasForeignKey(pe => pe.PreguntaId)
                 .OnDelete(DeleteBehavior.Restrict);

                // SetNull: si se elimina una Alternativa, la referencia en PreguntaExamen
                // queda en null en lugar de eliminar el registro del examen
                e.HasOne(pe => pe.AlternativaSeleccionada)
                 .WithMany(a => a.PreguntasExamen)
                 .HasForeignKey(pe => pe.AlternativaSeleccionadaId)
                 .OnDelete(DeleteBehavior.SetNull);

                // 500 chars alcanza para ~4 IDs de alternativas separados por coma
                e.Property(pe => pe.OrdenAlternativas).HasMaxLength(500).HasDefaultValue("");
            });

            // Porcentaje es una propiedad calculada en C#; no debe persistirse en la BD
            modelBuilder.Entity<Examen>().Ignore(e => e.Porcentaje);
        }
    }
}
