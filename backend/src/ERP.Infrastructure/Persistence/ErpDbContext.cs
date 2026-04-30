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
    /// Evaluada en cada query, no al compilar el modelo.
    /// Lanza UnauthorizedAccessException si el request no tiene tenant válido,
    /// evitando que Guid.Empty actúe como "ver todos los datos sin tenant".
    /// </summary>
    private Guid CurrentTenantId
    {
        get
        {
            var id = _currentTenant.TenantId;
            if (id == Guid.Empty)
                throw new UnauthorizedAccessException("Tenant no identificado. El request debe incluir un JWT válido.");
            return id;
        }
    }

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

        modelBuilder.Entity<User>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId);

        base.OnModelCreating(modelBuilder);
    }
}
