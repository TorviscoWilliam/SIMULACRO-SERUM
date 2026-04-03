using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Models;

namespace SimulacroExamen.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Usuario>           Usuarios           { get; set; }
        public DbSet<TipoExamen>        TiposExamen        { get; set; }
        public DbSet<UsuarioTipoExamen> UsuarioTiposExamen { get; set; }
        public DbSet<Pregunta>          Preguntas          { get; set; }
        public DbSet<Alternativa>       Alternativas       { get; set; }
        public DbSet<Examen>            Examenes           { get; set; }
        public DbSet<PreguntaExamen>    PreguntasExamen    { get; set; }
        public DbSet<Noticia>           Noticias           { get; set; }
        public DbSet<LogActividad>      LogsActividad      { get; set; }
        public DbSet<AnuncioGlobal>     AnunciosGlobales   { get; set; }
        public DbSet<PlanSuscripcion>   PlanesSuscripcion  { get; set; }
        public DbSet<Sugerencia>        Sugerencias        { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Usuarios ──────────────────────────────────────────────
            modelBuilder.Entity<Usuario>(e =>
            {
                e.HasIndex(u => u.NombreUsuario).IsUnique();
                e.HasIndex(u => u.Correo).IsUnique();
                e.Property(u => u.Contrasena).HasMaxLength(255);
                e.Property(u => u.FechaCreacion).HasDefaultValueSql("GETDATE()");
            });

            // ── TiposExamen ───────────────────────────────────────────
            modelBuilder.Entity<TipoExamen>(e =>
            {
                e.Property(t => t.Nombre).HasMaxLength(100);
                e.HasIndex(t => t.Nombre).IsUnique();
                e.Property(t => t.NumeroPreguntas).HasDefaultValue(4);
            });

            // ── UsuarioTiposExamen (tabla pivote Usuario <-> TipoExamen) ──
            modelBuilder.Entity<UsuarioTipoExamen>(e =>
            {
                // Un usuario no puede tener el mismo tipo asignado dos veces
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

                // SetNull: si se elimina el TipoExamen, la pregunta queda sin tipo
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

                // SetNull: si se elimina el tipo, el examen histórico queda sin tipo
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

                e.Property(pe => pe.OrdenAlternativas).HasMaxLength(500).HasDefaultValue("");
            });
        }
    }
}
