using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public StockAdjustmentRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public async Task AddAsync(StockAdjustment adjustment, CancellationToken ct = default) =>
        await _db.Set<StockAdjustment>().AddAsync(adjustment, ct);

    public Task<StockAdjustment?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db.Set<StockAdjustment>()
            .Include(a => a.Lines)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public async Task<StockAdjustment?> GetByIdForUpdateAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    )
    {
        // Patrón oficial (SalesInvoiceRepository.GetByIdForUpdateAsync): lock → lectura acotada → Reload.
        if (!_company.HasCompanyContext)
            return null;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM stock_adjustments WHERE tenant_id = {tenantId} AND company_id = {_company.CompanyId} AND id = {id} FOR UPDATE",
            ct
        );
        var adjustment = await GetByIdAsync(tenantId, id, ct);
        if (adjustment is not null)
            await _db.Entry(adjustment).ReloadAsync(ct);
        return adjustment;
    }

    /// <summary>
    /// El siguiente secuencial debe considerar también ajustes deshabilitados/soft-deleted (el
    /// filtro global de <see cref="StockAdjustment"/> los excluiría) — bypass explícito vía
    /// <see cref="PlatformQueryAccessor.AsPlatformQuery{T}"/>, con el filtro por TenantId
    /// reaplicado a mano en el mismo Where (ERP-CORE-CLOSEOUT-05-FIX02, P1-4).
    /// </summary>
    public async Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken ct = default)
    {
        var max = await _db.Set<StockAdjustment>()
            .AsPlatformQuery()
            .Where(a => a.TenantId == tenantId)
            .MaxAsync(a => (int?)a.Sequential, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        Guid? warehouseId,
        string? status,
        Guid? reasonId,
        string? movementType,
        DateOnly? startDate,
        DateOnly? endDate,
        IReadOnlyCollection<Guid>? branchWarehouseIds = null,
        CancellationToken ct = default
    )
    {
        var q = _db.Set<StockAdjustment>().Where(a => a.TenantId == tenantId);
        if (warehouseId.HasValue)
            q = q.Where(a => a.WarehouseId == warehouseId.Value);
        else if (branchWarehouseIds is not null)
            q = q.Where(a => branchWarehouseIds.Contains(a.WarehouseId));
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status);
        if (reasonId.HasValue)
            q = q.Where(a => a.ReasonId == reasonId.Value);
        if (!string.IsNullOrWhiteSpace(movementType))
            q = q.Where(a => a.MovementType == movementType);
        if (startDate.HasValue)
            q = q.Where(a => a.AdjustmentDate >= startDate.Value);
        if (endDate.HasValue)
            q = q.Where(a => a.AdjustmentDate <= endDate.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(a => a.Lines)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
