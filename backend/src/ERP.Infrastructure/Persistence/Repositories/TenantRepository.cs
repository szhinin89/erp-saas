using Microsoft.EntityFrameworkCore;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly ErpDbContext _context;

    public TenantRepository(ErpDbContext context) => _context = context;

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Tenants.IgnoreQueryFilters().OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == id, cancellationToken);

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
        => await _context.Tenants.AddAsync(tenant, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
