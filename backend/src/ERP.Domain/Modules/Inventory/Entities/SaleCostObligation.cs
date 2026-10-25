using ERP.Domain.Common;
using ERP.Domain.Exceptions;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>Projection of the append-only cost allocations of one original sale movement.</summary>
public sealed class SaleCostObligation : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid InvoiceLineId { get; private set; }
    public Guid SaleMovementId { get; private set; }
    public long SequenceNumber { get; private set; }
    public decimal? ProvisionalUnitCost { get; private set; }
    public decimal PendingQuantity { get; private set; }
    public decimal ResolvedQuantity { get; private set; }
    public decimal ResolvedCost { get; private set; }
    public decimal ReturnedQuantity { get; private set; }
    private SaleCostObligation() { }

    /// <param name="resolvedCost">INVENTORY-V1-B3 — importe monetario del tramo resuelto, tomado del
    /// mismo <see cref="StockMovement.TotalCost"/>; sin él se conserva cantidad × costo base.</param>
    public static SaleCostObligation Create(StockMovement movement, decimal pendingQuantity, decimal? basis,
        decimal? resolvedCost = null)
    {
        if (movement.SourceDocId is null || movement.SourceDocLineId is null)
            throw new DomainRuleViolationException("Sale costing requires the original document and line.");
        var quantity = Math.Abs(movement.Quantity);
        var result = new SaleCostObligation
        {
            Id = Guid.NewGuid(), TenantId = movement.TenantId, CompanyId = movement.CompanyId,
            ProductId = movement.ProductId, WarehouseId = movement.WarehouseId,
            InvoiceId = movement.SourceDocId.Value, InvoiceLineId = movement.SourceDocLineId.Value,
            SaleMovementId = movement.Id, SequenceNumber = movement.SequenceNumber,
            ProvisionalUnitCost = basis, PendingQuantity = pendingQuantity,
            ResolvedQuantity = quantity - pendingQuantity,
            ResolvedCost = resolvedCost ?? (quantity - pendingQuantity) * (basis ?? 0m)
        };
        result.SetCreated(movement.CreatedBy);
        return result;
    }

    /// <param name="coveredCost">INVENTORY-V1-B3 — importe monetario del tramo cubierto, parte del
    /// <see cref="StockMovement.TotalCost"/> de la entrada; sin él, cantidad × costo real.</param>
    public SaleCostAllocation Cover(StockMovement receipt, decimal quantity, decimal actualCost,
        decimal? coveredCost = null)
    {
        if (quantity <= 0m || quantity > PendingQuantity || actualCost < 0m)
            throw new DomainRuleViolationException("Invalid cost coverage.");
        var allocation = SaleCostAllocation.Create(this, receipt, quantity, 0m,
            ProvisionalUnitCost, actualCost, quantity * (actualCost - (ProvisionalUnitCost ?? 0m)), "Coverage");
        PendingQuantity -= quantity;
        ResolvedQuantity += quantity;
        ResolvedCost += coveredCost ?? quantity * actualCost;
        SetUpdated(receipt.CreatedBy);
        return allocation;
    }

    /// <param name="moneyDecimals">INVENTORY-V1-B3 — escala del importe resuelto devuelto (AccountingDecimals).</param>
    public SaleCostAllocation Return(StockMovement movement, decimal quantity, int? moneyDecimals = null)
    {
        if (quantity <= 0m || quantity > PendingQuantity + ResolvedQuantity)
            throw new DomainRuleViolationException("Return exceeds outstanding original sale units.");
        var pending = Math.Min(quantity, PendingQuantity);
        var resolved = quantity - pending;
        var resolvedCost = resolved == ResolvedQuantity ? ResolvedCost
            : ResolvedQuantity > 0m ? ResolvedCost * resolved / ResolvedQuantity : 0m;
        if (moneyDecimals is { } decimals && resolved != ResolvedQuantity)
            resolvedCost = Math.Round(resolvedCost, decimals, MidpointRounding.AwayFromZero);
        var recognizedCost = pending * (ProvisionalUnitCost ?? 0m) + resolvedCost;
        var allocation = SaleCostAllocation.Create(this, movement, pending, resolved,
            ProvisionalUnitCost, resolved > 0m ? resolvedCost / resolved : null, -recognizedCost, "Return");
        PendingQuantity -= pending;
        ResolvedQuantity -= resolved;
        ResolvedCost -= resolvedCost;
        ReturnedQuantity += quantity;
        SetUpdated(movement.CreatedBy);
        return allocation;
    }
}
