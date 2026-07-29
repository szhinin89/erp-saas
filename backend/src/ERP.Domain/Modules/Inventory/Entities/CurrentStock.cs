using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class CurrentStock : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal ReservedQuantity { get; private set; }
    public decimal AvailableQuantity => Quantity - ReservedQuantity;
    public decimal TotalStockValue { get; private set; }
    public decimal AverageCost => Quantity > 0m ? TotalStockValue / Quantity : 0m;
    public DateTime LastUpdatedAt { get; private set; }
    /// <summary>Optimistic concurrency token â€” EF Core uses this for concurrent update detection.</summary>
    public uint RowVersion { get; private set; }

    private CurrentStock() { }

    public static CurrentStock Create(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        Guid createdBy,
        Guid companyId)
    {
        var s = new CurrentStock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = 0,
            ReservedQuantity = 0,
            TotalStockValue = 0m,
            LastUpdatedAt = DateTime.UtcNow,
        };
        s.SetCreated(createdBy);
        return s;
    }

    public void ApplyMovement(decimal delta, Guid updatedBy, decimal unitCost = 0m)
    {
        var newQty = Quantity + delta;
        if (newQty < 0)
            throw new InvalidOperationException(
                $"Movement would leave stock at {newQty}. Insufficient stock.");

        TotalStockValue = Math.Max(0m, TotalStockValue + delta * unitCost);
        Quantity = newQty;
        LastUpdatedAt = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void Reserve(decimal quantity, Guid updatedBy)
    {
        if (quantity > AvailableQuantity)
            throw new InvalidOperationException(
                $"Insufficient available stock. Available: {AvailableQuantity}, requested: {quantity}.");
        ReservedQuantity += quantity;
        LastUpdatedAt = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void ReleaseReserve(decimal quantity, Guid updatedBy)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        LastUpdatedAt = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }
}
