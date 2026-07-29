using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class KardexSnapshot : ITenantScopedEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public DateTime SnapshotDate { get; private set; }
    public decimal BalanceQty { get; private set; }
    public decimal BalanceValue { get; private set; }
    public decimal AverageCost { get; private set; }
    public DateTime ComputedAt { get; private set; }

    private KardexSnapshot() { }

    public static KardexSnapshot Create(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        DateTime snapshotDate,
        decimal balanceQty,
        decimal balanceValue,
        decimal averageCost
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            SnapshotDate = DateTime.SpecifyKind(snapshotDate.Date, DateTimeKind.Utc),
            BalanceQty = balanceQty,
            BalanceValue = Math.Max(0m, balanceValue),
            AverageCost = averageCost,
            ComputedAt = DateTime.UtcNow,
        };
}
