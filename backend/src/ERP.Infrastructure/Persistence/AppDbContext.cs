using ERP.Domain.Entities; // Asegúrate de importar el namespace
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Aquí registras tu nueva entidad
    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Aquí irán configuraciones específicas (ej. nombres de columnas) más adelante
    }
}