using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly ErpDbContext _context;

    public WarehouseRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(Warehouse Warehouse, CancellationToken ct = default)
        => _context.Warehouses.AddAsync(Warehouse, ct).AsTask();

    public Task<Warehouse?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => _context.Warehouses.FirstOrDefaultAsync(b => b.SubscriberId == subscriberId && b.Id == id, ct);

    public async Task<bool> ExistsNameAsync(
        Guid subscriberId,
        string nombre,
        Guid? excludeId,
        CancellationToken ct = default)
    {
        var q = _context.Warehouses
            .Where(b => b.SubscriberId == subscriberId && b.Name == nombre.Trim());
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
        var q = _context.Warehouses.Where(b => b.SubscriberId == subscriberId);

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
