using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Models;

namespace SimulacroExamen.Data
{
    // IDataProtectionKeyContext permite que AddDataProtection().PersistKeysToDbContext()
    // guarde las claves criptográficas en la BD. Así sobreviven a reinicios del contenedor
    // en Railway y la contraseña SMTP cifrada sigue siendo descifrable entre deploys.
    public class ApplicationDbContext : DbContext, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Tabla donde DataProtection guarda sus claves XML
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        // ── Jerarquía TPH de Usuarios ─────────────────────────────────
        public DbSet<Usuario>       Usuarios        { get; set; }
        public DbSet<Administrador> Administradores { get; set; }
        public DbSet<Estudiante>    Estudiantes     { get; set; }

        public DbSet<TipoExamen>             TiposExamen             { get; set; }
        public DbSet<UsuarioTipoExamen>      UsuarioTiposExamen      { get; set; }
        public DbSet<Pregunta>               Preguntas               { get; set; }
        public DbSet<Alternativa>            Alternativas            { get; set; }
        public DbSet<Examen>                 Examenes                { get; set; }
        public DbSet<PreguntaExamen>         PreguntasExamen         { get; set; }
        public DbSet<OrdenAlternativaExamen> OrdenesAlternativaExamen { get; set; }
        public DbSet<Noticia>                Noticias                { get; set; }
        public DbSet<LogActividad>           LogsActividad           { get; set; }
        public DbSet<AnuncioGlobal>          AnunciosGlobales        { get; set; }
        public DbSet<PlanSuscripcion>        PlanesSuscripcion       { get; set; }
        public DbSet<CaracteristicaPlan>     CaracteristicasPlan     { get; set; }
        public DbSet<Sugerencia>             Sugerencias             { get; set; }
        public DbSet<ConfiguracionCorreo>    ConfiguracionCorreo     { get; set; }
        public DbSet<OpcionDuracion>         OpcionesDuracion        { get; set; }
        public DbSet<ParametrosGlobales>      ParametrosGlobales      { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Usuarios: TPH (Table Per Hierarchy) ───────────────────
            // Una sola tabla "Usuarios" con columna "Discriminador" que
            // indica el tipo concreto: "Administrador" o "Estudiante".
            // El campo Rol sigue siendo una propiedad normal (para auth claims).
            modelBuilder.Entity<Usuario>(e =>
            {
                e.HasIndex(u => u.NombreUsuario).IsUnique();
                e.HasIndex(u => u.Correo).IsUnique();
                e.Property(u => u.Contrasena).HasMaxLength(255);
                e.Property(u => u.FechaCreacion).HasDefaultValueSql("GETDATE()");

                e.HasDiscriminator<string>("Discriminador")
                 .HasValue<Administrador>("Administrador")
                 .HasValue<Estudiante>("Estudiante");
            });

            // ── TiposExamen ───────────────────────────────────────────
            modelBuilder.Entity<TipoExamen>(e =>
            {
                e.Property(t => t.Nombre).HasMaxLength(100);
                e.HasIndex(t => t.Nombre).IsUnique();
                e.Property(t => t.NumeroPreguntas).HasDefaultValue(4);
            });

            // ── UsuarioTiposExamen ────────────────────────────────────
            modelBuilder.Entity<UsuarioTipoExamen>(e =>
            {
                e.HasIndex(ut => new { ut.UsuarioId, ut.TipoExamenId }).IsUnique();

                // Cascade: si se elimina el usuario, sus asignaciones de tipo de examen
                // ya no tienen sentido y deben eliminarse con él.
                e.HasOne(ut => ut.Usuario)
                 .WithMany(u => u.UsuariosTipoExamen)
                 .HasForeignKey(ut => ut.UsuarioId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Cascade: si se elimina un tipo de examen, las asignaciones de ese
                // tipo a usuarios se eliminan automáticamente para mantener integridad.
                e.HasOne(ut => ut.TipoExamen)
                 .WithMany(t => t.UsuariosTipoExamen)
                 .HasForeignKey(ut => ut.TipoExamenId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(ut => ut.FechaAsignacion).HasDefaultValueSql("GETDATE()");
            });

            // ── Preguntas ─────────────────────────────────────────────
            modelBuilder.Entity<Pregunta>(e =>
            {
                e.Property(p => p.FechaCreacion).HasDefaultValueSql("GETDATE()");
                e.Property(p => p.TextoPregunta).HasColumnType("nvarchar(max)");

                // SetNull: si se elimina un tipo de examen, las preguntas se conservan
                // (son un activo de contenido) y quedan sin tipo asignado (TipoExamenId = NULL)
                // para que el admin las reasigne o las archive manualmente.
                e.HasOne(p => p.TipoExamen)
                 .WithMany(t => t.Preguntas)
                 .HasForeignKey(p => p.TipoExamenId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Alternativas ──────────────────────────────────────────
            modelBuilder.Entity<Alternativa>(e =>
            {
                // Cascade: las alternativas son parte inseparable de la pregunta;
                // al eliminar una pregunta sus alternativas pierden todo contexto y
                // deben eliminarse junto con ella.
                e.HasOne(a => a.Pregunta)
                 .WithMany(p => p.Alternativas)
                 .HasForeignKey(a => a.PreguntaId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(a => a.TextoAlternativa).HasColumnType("nvarchar(max)");
            });

            // ── Examenes ──────────────────────────────────────────────
            modelBuilder.Entity<Examen>(e =>
            {
                // Restrict: no se puede eliminar un usuario que tenga exámenes registrados.
                // Los exámenes son el historial académico del estudiante y no deben perderse
                // por un borrado accidental; se requiere eliminar primero los exámenes o
                // desactivar la cuenta en lugar de eliminarla.
                e.HasOne(ex => ex.Usuario)
                 .WithMany(u => u.Examenes)
                 .HasForeignKey(ex => ex.UsuarioId)
                 .OnDelete(DeleteBehavior.Restrict);

                // SetNull: si se elimina un tipo de examen, los exámenes ya rendidos
                // conservan sus datos históricos (calificaciones, respuestas) pero quedan
                // sin tipo asignado (TipoExamenId = NULL) para no perder el registro.
                e.HasOne(ex => ex.TipoExamen)
                 .WithMany(t => t.Examenes)
                 .HasForeignKey(ex => ex.TipoExamenId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.Property(ex => ex.FechaInicio).HasDefaultValueSql("GETDATE()");
                e.Ignore(ex => ex.Porcentaje);
            });

            // ── Noticias ──────────────────────────────────────────────
            modelBuilder.Entity<Noticia>(e =>
            {
                e.Property(n => n.FechaPublicacion).HasDefaultValueSql("GETDATE()");
                // Restrict: las noticias publicadas tienen valor histórico para los
                // estudiantes; no deben borrarse en cascada si se elimina el admin autor.
                // Se debe reasignar o archivar la noticia antes de eliminar al admin.
                e.HasOne(n => n.Admin)
                 .WithMany()
                 .HasForeignKey(n => n.AdminId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── PreguntasExamen ───────────────────────────────────────
            modelBuilder.Entity<PreguntaExamen>(e =>
            {
                // Cascade: las preguntas de un examen son parte inseparable del examen;
                // al eliminar el examen ya no tiene sentido conservar sus filas hijas.
                e.HasOne(pe => pe.Examen)
                 .WithMany(ex => ex.PreguntasExamen)
                 .HasForeignKey(pe => pe.ExamenId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Restrict: se conserva la pregunta del banco aunque se borre la instancia
                // del examen. Evita también que un borrado de Pregunta destruya silenciosamente
                // el historial de exámenes ya finalizados; debe depurarse explícitamente.
                e.HasOne(pe => pe.Pregunta)
                 .WithMany(p => p.PreguntasExamen)
                 .HasForeignKey(pe => pe.PreguntaId)
                 .OnDelete(DeleteBehavior.Restrict);

                // SetNull: si la alternativa seleccionada se elimina (ej. corrección del banco
                // de preguntas), la respuesta del alumno queda registrada como NULL (sin
                // alternativa) en lugar de borrarse la fila entera y perder la trazabilidad.
                e.HasOne(pe => pe.AlternativaSeleccionada)
                 .WithMany(a => a.PreguntasExamen)
                 .HasForeignKey(pe => pe.AlternativaSeleccionadaId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── OpcionDuracion ────────────────────────────────────────
            modelBuilder.Entity<OpcionDuracion>(e =>
            {
                // Cascade: las opciones de duración son configuración del tipo de examen;
                // sin su tipo de examen no tienen utilidad ni contexto, por lo que se
                // eliminan automáticamente junto con él.
                e.HasOne(o => o.TipoExamen)
                 .WithMany(t => t.OpcionesDuracion)
                 .HasForeignKey(o => o.TipoExamenId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(o => o.Etiqueta).HasMaxLength(100);
                e.Property(o => o.DuracionMinutos).HasDefaultValue(0);
                e.Property(o => o.NumeroPreguntas).HasDefaultValue(0);
            });

            // ── OrdenAlternativaExamen ────────────────────────────────
            modelBuilder.Entity<OrdenAlternativaExamen>(e =>
            {
                // Cascade: el orden de alternativas es metadata de la instancia de
                // PreguntaExamen; al eliminar la pregunta del examen, su orden de
                // presentación ya no tiene sentido y debe eliminarse con ella.
                e.HasOne(o => o.PreguntaExamen)
                 .WithMany(pe => pe.OrdenAlternativasExamen)
                 .HasForeignKey(o => o.PreguntaExamenId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Restrict: la alternativa del banco no debe borrarse automáticamente
                // solo porque se elimine un orden de presentación de examen. SQL Server
                // rechazaría múltiples cascadas desde Alternativa de todas formas.
                e.HasOne(o => o.Alternativa)
                 .WithMany()
                 .HasForeignKey(o => o.AlternativaId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── LogActividad ──────────────────────────────────────────
            modelBuilder.Entity<LogActividad>(e =>
            {
                // Restrict: los logs de auditoría son inmutables por diseño; eliminar
                // un admin no debe borrar su rastro de acciones. El log debe conservarse
                // para trazabilidad y cumplimiento; se requiere acción manual para purgar.
                e.HasOne(l => l.Admin)
                 .WithMany()
                 .HasForeignKey(l => l.AdminId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── CaracteristicaPlan ────────────────────────────────────
            modelBuilder.Entity<CaracteristicaPlan>(e =>
            {
                e.HasOne(c => c.Plan)
                 .WithMany(p => p.Caracteristicas)
                 .HasForeignKey(c => c.PlanSuscripcionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Estudiante → PlanSuscripcion ──────────────────────────
            // Al borrar un plan, los estudiantes que lo tenían quedan
            // sin plan asignado (SetNull) pero no pierden el acceso.
            modelBuilder.Entity<Estudiante>(e =>
            {
                e.HasOne(est => est.PlanSuscripcion)
                 .WithMany()
                 .HasForeignKey(est => est.PlanSuscripcionId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ParametrosGlobales>(e =>
            {
                e.HasOne(p => p.Admin)
                 .WithMany()
                 .HasForeignKey(p => p.AdminId)
                 .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
