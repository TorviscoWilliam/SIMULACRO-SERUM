using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Models;

namespace SimulacroExamen.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

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

                e.HasOne(ut => ut.Usuario)
                 .WithMany(u => u.UsuariosTipoExamen)
                 .HasForeignKey(ut => ut.UsuarioId)
                 .OnDelete(DeleteBehavior.Cascade);

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

                e.HasOne(p => p.TipoExamen)
                 .WithMany(t => t.Preguntas)
                 .HasForeignKey(p => p.TipoExamenId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Alternativas ──────────────────────────────────────────
            modelBuilder.Entity<Alternativa>(e =>
            {
                e.HasOne(a => a.Pregunta)
                 .WithMany(p => p.Alternativas)
                 .HasForeignKey(a => a.PreguntaId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(a => a.TextoAlternativa).HasColumnType("nvarchar(max)");
            });

            // ── Examenes ──────────────────────────────────────────────
            modelBuilder.Entity<Examen>(e =>
            {
                e.HasOne(ex => ex.Usuario)
                 .WithMany(u => u.Examenes)
                 .HasForeignKey(ex => ex.UsuarioId)
                 .OnDelete(DeleteBehavior.Restrict);

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
                e.HasOne(n => n.Admin)
                 .WithMany()
                 .HasForeignKey(n => n.AdminId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── PreguntasExamen ───────────────────────────────────────
            modelBuilder.Entity<PreguntaExamen>(e =>
            {
                e.HasOne(pe => pe.Examen)
                 .WithMany(ex => ex.PreguntasExamen)
                 .HasForeignKey(pe => pe.ExamenId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(pe => pe.Pregunta)
                 .WithMany(p => p.PreguntasExamen)
                 .HasForeignKey(pe => pe.PreguntaId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(pe => pe.AlternativaSeleccionada)
                 .WithMany(a => a.PreguntasExamen)
                 .HasForeignKey(pe => pe.AlternativaSeleccionadaId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── OrdenAlternativaExamen ────────────────────────────────
            modelBuilder.Entity<OrdenAlternativaExamen>(e =>
            {
                e.HasOne(o => o.PreguntaExamen)
                 .WithMany(pe => pe.OrdenAlternativasExamen)
                 .HasForeignKey(o => o.PreguntaExamenId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(o => o.Alternativa)
                 .WithMany()
                 .HasForeignKey(o => o.AlternativaId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── LogActividad ──────────────────────────────────────────
            modelBuilder.Entity<LogActividad>(e =>
            {
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
        }
    }
}
