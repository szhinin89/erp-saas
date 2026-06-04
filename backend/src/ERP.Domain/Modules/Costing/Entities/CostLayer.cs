using ERP.Domain.Common;
using ERP.Domain.Modules.Costing.Enums;

namespace ERP.Domain.Modules.Costing.Entities;

/// <summary>
/// Aggregate Root. Capa de costo de entrada (AVCO / FIFO / LIFO).
/// Scope: company.
/// INVARIANTE: RemainingQty ∈ [0, EntryQty]. IsExhausted ↔ RemainingQty == 0.
/// REGLA: Solo se crea desde PurchaseReceipt.Confirm() o ImportOrder.Allocate().
/// </summary>
public sealed class CostLayer : AuditableEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? LotId { get; private set; }
    public CostMethod CostMethod { get; private set; }
    public DateTime LayerDate { get; private set; }
    public Guid SourceDocumentId { get; private set; }
    public string SourceDocumentType { get; private set; } = null!;
    public decimal EntryQty { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal RemainingQty { get; private set; }
    public bool IsExhausted { get; private set; }

    private CostLayer() { }

    public static CostLayer Create(
        Guid subscriberId,
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        Guid sourceDocumentId,
        string sourceDocumentType,
        decimal entryQty,
        decimal unitCost,
        CostMethod costMethod,
        Guid createdBy,
        Guid? variantId = null,
        Guid? lotId = null,
        DateTime? layerDate = null)
    {
        if (entryQty <= 0)
            throw new ArgumentException("La cantidad de entrada debe ser mayor que cero.", nameof(entryQty));
        if (unitCost < 0)
            throw new ArgumentException("El costo unitario no puede ser negativo.", nameof(unitCost));
        if (string.IsNullOrWhiteSpace(sourceDocumentType))
            throw new ArgumentException("El tipo de documento origen es obligatorio.", nameof(sourceDocumentType));

        var layer = new CostLayer
        {
            SubscriberId       = subscriberId,
            CompanyId          = companyId,
            ItemId             = itemId,
            VariantId          = variantId,
            WarehouseId        = warehouseId,
            LotId              = lotId,
            CostMethod         = costMethod,
            LayerDate          = layerDate ?? DateTime.UtcNow,
            SourceDocumentId   = sourceDocumentId,
            SourceDocumentType = sourceDocumentType.Trim(),
            EntryQty           = entryQty,
            UnitCost           = unitCost,
            TotalCost          = entryQty * unitCost,
            RemainingQty       = entryQty,
            IsExhausted        = false,
        };
        layer.SetCreated(createdBy);
        return layer;
    }

    public void Consume(decimal qty)
    {
        if (IsExhausted)
            throw new InvalidOperationException("La capa de costo ya está agotada.");
        if (qty > RemainingQty)
            throw new InvalidOperationException($"Cantidad insuficiente en capa de costo. Disponible: {RemainingQty}, requerido: {qty}.");
        if (qty <= 0)
            throw new ArgumentException("La cantidad a consumir debe ser mayor que cero.", nameof(qty));

        RemainingQty -= qty;

        if (RemainingQty == 0)
            IsExhausted = true;
    }

    public void UpdateUnitCost(decimal newUnitCost, Guid updatedBy)
    {
        if (newUnitCost < 0)
            throw new ArgumentException("El costo unitario no puede ser negativo.", nameof(newUnitCost));

        UnitCost  = newUnitCost;
        TotalCost = EntryQty * newUnitCost;
        SetUpdated(updatedBy);
    }
}
