using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class InventoryAdjustmentReasonRepository : IInventoryAdjustmentReasonRepository
{
    private readonly ErpDbContext _db;

    public InventoryAdjustmentReasonRepository(ErpDbContext db) => _db = db;

    public async Task AddAsync(
        InventoryAdjustmentReason reason,
        CancellationToken ct = default
    ) => await _db.Set<InventoryAdjustmentReason>().AddAsync(reason, ct);

    public Task<InventoryAdjustmentReason?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db.Set<InventoryAdjustmentReason>()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public Task<InventoryAdjustmentReason?> GetByCodeAsync(
        Guid tenantId,
        string code,
        CancellationToken ct = default
    ) =>
        _db.Set<InventoryAdjustmentReason>()
            .AsPlatformQuery()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.Code == code.Trim().ToUpper(),
                ct
            );

    public async Task<IReadOnlyList<InventoryAdjustmentReason>> ListAsync(
        Guid tenantId,
        Guid? companyId,
        bool includeInactive,
        CancellationToken ct = default
    )
    {
        var q = _db.Set<InventoryAdjustmentReason>().Where(r => r.TenantId == tenantId);
        if (!includeInactive)
            q = q.Where(r => r.IsActive);
        if (companyId.HasValue)
            q = q.Where(r => r.CompanyId == null || r.CompanyId == companyId.Value);

        return await q.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
