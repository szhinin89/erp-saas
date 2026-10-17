using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

/// <summary>ACCOUNTING-12A — implementación de <see cref="IInventoryAdjustmentReasonAccountRepository"/>.</summary>
public sealed class InventoryAdjustmentReasonAccountRepository(ErpDbContext db)
    : IInventoryAdjustmentReasonAccountRepository
{
    public Task<InventoryAdjustmentReasonAccount?> GetAsync(
        Guid tenantId, Guid companyId, Guid inventoryAdjustmentReasonId, CancellationToken ct = default) =>
        db.InventoryAdjustmentReasonAccounts.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.CompanyId == companyId
                && x.InventoryAdjustmentReasonId == inventoryAdjustmentReasonId, ct);

    public async Task<IReadOnlyDictionary<Guid, InventoryAdjustmentReasonAccount>> GetMapAsync(
        Guid tenantId, Guid companyId, CancellationToken ct = default) =>
        (await db.InventoryAdjustmentReasonAccounts
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .ToListAsync(ct))
        .ToDictionary(x => x.InventoryAdjustmentReasonId);

    public Task AddAsync(InventoryAdjustmentReasonAccount entity, CancellationToken ct = default) =>
        db.InventoryAdjustmentReasonAccounts.AddAsync(entity, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
