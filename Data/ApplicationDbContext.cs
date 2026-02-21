using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Models;

namespace SimulacroExamen.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Usuario>        Usuarios        { get; set; }
        public DbSet<Pregunta>       Preguntas       { get; set; }
        public DbSet<Alternativa>    Alternativas    { get; set; }
        public DbSet<Examen>         Examenes        { get; set; }
        public DbSet<PreguntaExamen> PreguntasExamen { get; set; }

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

            // ── Preguntas ─────────────────────────────────────────────
            modelBuilder.Entity<Pregunta>(e =>
            {
                e.Property(p => p.FechaCreacion).HasDefaultValueSql("GETDATE()");
                e.Property(p => p.TextoPregunta).HasColumnType("nvarchar(max)");
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

                e.Property(ex => ex.FechaInicio).HasDefaultValueSql("GETDATE()");
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

            // Ignorar propiedad calculada
            modelBuilder.Entity<Examen>().Ignore(e => e.Porcentaje);
        }
    }
}
