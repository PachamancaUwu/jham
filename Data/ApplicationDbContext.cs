using Microsoft.EntityFrameworkCore;
using jhampro.Models;

public class ApplicationDbContext : DbContext
{

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Servicio> Servicios { get; set; }
    public DbSet<AbogadoServicio> AbogadoServicio { get; set; }
    
    public DbSet<Documento> Documentos { get; set; }
    public DbSet<Retroalimentacion> Retroalimentaciones { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Relación uno a muchos (Cliente a Servicio)
        modelBuilder.Entity<Servicio>()
            .HasOne(s => s.Cliente)
            .WithMany(u => u.ServiciosComoCliente)
            .HasForeignKey(s => s.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relación muchos a muchos (Abogado a Servicio) - Clave compuesta
        modelBuilder.Entity<AbogadoServicio>()
            .HasKey(us => new { us.UsuarioId, us.ServicioId });

        modelBuilder.Entity<AbogadoServicio>()
            .HasOne(us => us.Abogado)
            .WithMany(u => u.AbogadoServicios)
            .HasForeignKey(us => us.UsuarioId);

        modelBuilder.Entity<AbogadoServicio>()
            .HasOne(us => us.Servicio)
            .WithMany(s => s.AbogadoServicios)
            .HasForeignKey(us => us.ServicioId);

        // Relación uno a uno (Pago a Servicio)
        modelBuilder.Entity<Servicio>()
            .HasOne(s => s.Pago)
            .WithOne(p => p.Servicio)
            .HasForeignKey<Pago>(p => p.ServicioId);

        // Relación uno a muchos (Servicio a Retroalimentaciones)
        modelBuilder.Entity<Servicio>()
            .HasMany(s => s.Retroalimentaciones)
            .WithOne(r => r.Servicio)
            .HasForeignKey(r => r.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relación uno a muchos (Servicio a Documento)
        modelBuilder.Entity<Documento>()
            .HasOne(d => d.Servicio)
            .WithMany(s => s.Documentos)
            .HasForeignKey(d => d.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);

        base.OnModelCreating(modelBuilder);
    }

}