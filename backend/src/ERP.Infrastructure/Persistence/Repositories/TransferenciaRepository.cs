using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class TransferenciaRepository : IStockTransferRepository
{
    private readonly ErpDbContext _context;

    public TransferenciaRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(StockTransfer StockTransfer, CancellationToken ct = default)
        => _context.StockTransfers.AddAsync(StockTransfer, ct).AsTask();

    public Task<StockTransfer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.StockTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.TargetWarehouse)
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public async Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken ct = default)
    {
        // MaxAsync on empty sequence throws; use nullable Max then coalesce.
        // Also compatible with EF InMemory (DefaultIfEmpty+MaxAsync not translatable there).
        var max = await _context.StockTransfers
            .Where(t => t.TenantId == tenantId)
            .MaxAsync(t => (int?)t.Sequential, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<StockTransfer> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     WarehouseOrigenId,
        Guid?     WarehouseDestinoId,
        string?   estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default)
    {
        var query = _context.StockTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.TargetWarehouse)
            .Where(t => t.TenantId == tenantId);

        if (WarehouseOrigenId.HasValue)
            query = query.Where(t => t.SourceWarehouseId == WarehouseOrigenId.Value);
        if (WarehouseDestinoId.HasValue)
            query = query.Where(t => t.TargetWarehouseId == WarehouseDestinoId.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(t => t.Status == estado);
        if (fechaDesde.HasValue)
            query = query.Where(t => t.TransferDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(t => t.TransferDate <= fechaHasta.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.TransferDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

