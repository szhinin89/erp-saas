using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class StockTransferRepository : IStockTransferRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public StockTransferRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<StockTransfer> Scoped(Guid subscriberId)
        => _context.StockTransfers.ForOperationalScope(subscriberId, _company);

    public Task AddAsync(StockTransfer StockTransfer, CancellationToken ct = default)
        => _context.StockTransfers.AddAsync(StockTransfer, ct).AsTask();

    public Task<StockTransfer?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => Scoped(subscriberId)
            .Include(t => t.SourceWarehouse)
            .Include(t => t.TargetWarehouse)
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<int> GetNextSequentialAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var max = await Scoped(subscriberId).MaxAsync(t => (int?)t.Sequential, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<StockTransfer> Items, int TotalCount)> GetPagedAsync(
        Guid      subscriberId,
        int       pageNumber,
        int       pageSize,
        Guid?     WarehouseOrigenId,
        Guid?     WarehouseDestinoId,
        string? status,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default)
    {
        IQueryable<StockTransfer> query = Scoped(subscriberId);

        if (WarehouseOrigenId.HasValue)
            query = query.Where(t => t.SourceWarehouseId == WarehouseOrigenId.Value);
        if (WarehouseDestinoId.HasValue)
            query = query.Where(t => t.TargetWarehouseId == WarehouseDestinoId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);
        if (fechaDesde.HasValue)
            query = query.Where(t => t.TransferDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(t => t.TransferDate <= fechaHasta.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(t => t.SourceWarehouse)
            .Include(t => t.TargetWarehouse)
            .OrderByDescending(t => t.TransferDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
