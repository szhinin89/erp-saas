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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

        // Filtro global multi-tenant — se aplica automaticamente en todas las queries
        var tenantId = _currentTenant.TenantId;

        modelBuilder.Entity<Account>()
            .HasQueryFilter(e => e.TenantId == tenantId);

        modelBuilder.Entity<JournalEntry>()
            .HasQueryFilter(e => e.TenantId == tenantId);

        modelBuilder.Entity<JournalEntryLine>()
            .HasQueryFilter(e => e.TenantId == tenantId);

        modelBuilder.Entity<Product>()
            .HasQueryFilter(e => e.TenantId == tenantId);

        base.OnModelCreating(modelBuilder);
    }
}

