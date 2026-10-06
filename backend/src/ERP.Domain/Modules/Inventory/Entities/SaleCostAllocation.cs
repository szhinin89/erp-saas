using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>Immutable receipt/return to original-sale allocation. Amount is the signed COGS adjustment.</summary>
public sealed class SaleCostAllocation : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ObligationId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid InvoiceLineId { get; private set; }
    public Guid SaleMovementId { get; private set; }
    public Guid OriginMovementId { get; private set; }
    public Guid? OriginDocumentId { get; private set; }
    public Guid? OriginLineId { get; private set; }
    public decimal PendingQuantity { get; private set; }
    public decimal ResolvedQuantity { get; private set; }
    public decimal? PreviousUnitCost { get; private set; }
    public decimal? ActualUnitCost { get; private set; }
    public decimal CogsAdjustment { get; private set; }
    public string Kind { get; private set; } = null!;
    private SaleCostAllocation() { }

    internal static SaleCostAllocation Create(SaleCostObligation obligation, StockMovement origin,
        decimal pendingQuantity, decimal resolvedQuantity, decimal? previousCost, decimal? actualCost,
        decimal adjustment, string kind)
    {
        var result = new SaleCostAllocation
        {
            Id = Guid.NewGuid(), TenantId = obligation.TenantId, CompanyId = obligation.CompanyId,
            ProductId = obligation.ProductId, WarehouseId = obligation.WarehouseId,
            ObligationId = obligation.Id, InvoiceId = obligation.InvoiceId, InvoiceLineId = obligation.InvoiceLineId,
            SaleMovementId = obligation.SaleMovementId, OriginMovementId = origin.Id,
            OriginDocumentId = origin.SourceDocId, OriginLineId = origin.SourceDocLineId,
            PendingQuantity = pendingQuantity, ResolvedQuantity = resolvedQuantity,
            PreviousUnitCost = previousCost, ActualUnitCost = actualCost, CogsAdjustment = adjustment, Kind = kind
        };
        result.SetCreated(origin.CreatedBy);
        return result;
    }
}
