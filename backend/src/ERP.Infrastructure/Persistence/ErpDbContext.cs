using Microsoft.EntityFrameworkCore;
using ERP.Domain.Accounting.Entities;
using ERP.Domain.Products.Entities;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Application.Common;

namespace ERP.Infrastructure.Persistence;

public class ErpDbContext : DbContext
{
    private readonly ICurrentTenant _currentTenant;

    public ErpDbContext(
        DbContextOptions<ErpDbContext> options,
        ICurrentTenant currentTenant) : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>
    /// Propiedad de instancia evaluada en cada query.
    /// NO usar una variable local en OnModelCreating: EF Core compila el modelo
    /// una sola vez por aplicación, por lo que una variable local capturaría el
    /// valor en startup y todos los tenants verían los mismos datos.
    /// </summary>
    private Guid CurrentTenantId => _currentTenant.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

        // Filtros globales de aislamiento multi-tenant.
        // Al agregar una nueva entidad con TenantId, registrar su filtro aquí.
        modelBuilder.Entity<Account>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId);

        modelBuilder.Entity<JournalEntry>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId);

        modelBuilder.Entity<JournalEntryLine>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId);

        modelBuilder.Entity<Product>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId);

        base.OnModelCreating(modelBuilder);
    }
}
