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

    private IQueryable<Warehouse> Scoped(Guid subscriberId)
        => _context.Warehouses.ForOperationalScope(subscriberId, _company);

    public Task AddAsync(Warehouse Warehouse, CancellationToken ct = default)
        => _context.Warehouses.AddAsync(Warehouse, ct).AsTask();

    public Task<Warehouse?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => Scoped(subscriberId).FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<bool> ExistsNameAsync(
        Guid subscriberId,
        string nombre,
        Guid? excludeId,
        CancellationToken ct = default)
    {
        var q = Scoped(subscriberId).Where(b => b.Name == nombre.Trim());
        if (excludeId.HasValue)
            q = q.Where(b => b.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<Warehouse>> GetAsync(
        Guid subscriberId,
        bool? activeFilter,
        string? search,
        Guid? sucursalId,
        CancellationToken ct = default)
    {
        var q = Scoped(subscriberId);

        if (activeFilter is true)  q = q.Where(b => b.IsActive);
        else if (activeFilter is false) q = q.Where(b => !b.IsActive);

        if (sucursalId.HasValue) q = q.Where(b => b.BranchId == sucursalId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(b =>
                b.Name.ToLower().Contains(s) ||
                (b.Address != null && b.Address.ToLower().Contains(s)) ||
                (b.Manager != null && b.Manager.ToLower().Contains(s)));
        }

        return await q.OrderBy(b => b.Name).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
