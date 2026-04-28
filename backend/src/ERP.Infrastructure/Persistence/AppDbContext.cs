using Microsoft.EntityFrameworkCore;
using ERP.Domain.Products.Entities;
// Agrega aquí otras entidades según vayas creando:
// using ERP.Domain.Accounting.Entities;

namespace ERP.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) 
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        // public DbSet<Account> Accounts { get; set; }
        // public DbSet<JournalEntry> JournalEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Aquí irán las configuraciones de entidades (Fluent API)
        }
    }
}