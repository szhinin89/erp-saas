using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public WarehouseRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<Warehouse> Scoped(Guid tenantId)
        => _context.Warehouses.ForOperationalScope(tenantId, _company);

    public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
        => _context.Warehouses.AddAsync(warehouse, cancellationToken).AsTask();

    public Task<Warehouse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => Scoped(tenantId).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<bool> ExistsCodeAsync(
        Guid tenantId,
        Guid branchId,
        string code,
        Guid? excludeId,
        CancellationToken cancellationToken = default)
    {
        var q = Scoped(tenantId).Where(w => w.BranchId == branchId && w.Code == code.Trim());
        if (excludeId.HasValue)
            q = q.Where(w => w.Id != excludeId.Value);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Warehouse>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var q = Scoped(tenantId);

        if (activeFilter is true)       q = q.Where(b => b.IsActive);
        else if (activeFilter is false) q = q.Where(b => !b.IsActive);

        if (branchId.HasValue) q = q.Where(b => b.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(b =>
                b.Name.Contains(s) ||
                (b.Code    != null && b.Code.Contains(s)) ||
                (b.Address != null && b.Address.Contains(s)) ||
                (b.Manager != null && b.Manager.Contains(s)));
        }

        return await q.OrderBy(b => b.Name).ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
