using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public StockAdjustmentRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<StockAdjustment> Scoped(Guid subscriberId)
        => _context.StockAdjustments.ForOperationalScope(subscriberId, _company);

    public Task AddAsync(StockAdjustment ajuste, CancellationToken ct = default)
        => _context.StockAdjustments.AddAsync(ajuste, ct).AsTask();

    public Task<StockAdjustment?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => Scoped(subscriberId).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<int> GetNextSequentialAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var max = await Scoped(subscriberId).MaxAsync(a => (int?)a.Sequential, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        Guid      subscriberId,
        int       pageNumber,
        int       pageSize,
        Guid?     WarehouseId,
        Guid?     productoId,
        string? status,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default)
    {
        var query = Scoped(subscriberId);

        if (WarehouseId.HasValue)
            query = query.Where(a => a.WarehouseId == WarehouseId.Value);
        if (productoId.HasValue)
            query = query.Where(a => a.ProductId == productoId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);
        if (fechaDesde.HasValue)
            query = query.Where(a => a.AdjustmentDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(a => a.AdjustmentDate <= fechaHasta.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.AdjustmentDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
