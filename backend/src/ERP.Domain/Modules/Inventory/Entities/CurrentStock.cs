using ERP.Domain.Common;
using ERP.Domain.Exceptions;

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
    public decimal AverageCost => Quantity > 0m ? TotalStockValue / Quantity : CostBasis ?? 0m;
    public decimal? CostBasis { get; private set; }
    public bool CostPending { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token — EF Core uses this for concurrent update detection.</summary>
    public uint RowVersion { get; private set; }

    private CurrentStock() { }

    public static CurrentStock Create(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        Guid createdBy,
        Guid companyId
    )
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
            throw new DomainRuleViolationException(
                $"Movement would leave stock at {newQty}. Insufficient stock."
            );

        TotalStockValue = Math.Max(0m, TotalStockValue + delta * unitCost);
        Quantity = newQty;
        LastUpdatedAt = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void ApplyKardexMovement(StockMovement movement, bool authorizedNegativeSale, bool enforceAvailability)
    {
        if (movement.ProductId != ProductId || movement.WarehouseId != WarehouseId
            || movement.CompanyId != CompanyId || movement.TenantId != TenantId
            || movement.PreviousQuantity != Quantity)
            throw new DomainRuleViolationException("Inventory movement does not match its current stock.");
        var sale = movement.MovementType == ERP.Domain.Modules.Inventory.Enums.StockMovementType.SaleExit;
        if (authorizedNegativeSale && !sale)
            throw new DomainRuleViolationException("Only an authorized SaleExit can consume negative stock.");
        if (sale && enforceAvailability && AvailableQuantity < -movement.Quantity)
            throw new DomainRuleViolationException("Insufficient available stock.");
        // Positive entries may compensate an existing negative balance partially; they never create a deficit.
        if (movement.ResultQuantity < 0m && movement.Quantity < 0m && !(sale && authorizedNegativeSale))
            throw new DomainRuleViolationException("Movement would leave insufficient stock.");
        Quantity = movement.ResultQuantity;
        TotalStockValue = movement.RunningStockValue;
        CostBasis = movement.CostBasis;
        CostPending = movement.CostPending;
        LastUpdatedAt = DateTime.UtcNow;
        SetUpdated(movement.CreatedBy);
    }

    public void Reserve(decimal quantity, Guid updatedBy)
    {
        if (quantity > AvailableQuantity)
            throw new DomainRuleViolationException(
                $"Insufficient available stock. Available: {AvailableQuantity}, requested: {quantity}."
            );
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
