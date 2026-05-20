using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseOrderLine : AuditableEntity, ISubscriberScopedEntity
{
    public const int DescriptionMaxLen = 300;

    public Guid    PurchaseOrderId   { get; private set; }
    public Guid    ProductId         { get; private set; }
    public string  Description       { get; private set; } = null!;
    public decimal OrderedQty        { get; private set; }
    public decimal InvoicedQty       { get; private set; }
    public decimal UnitCost          { get; private set; }
    public decimal Subtotal          { get; private set; }
    public decimal TaxAmount         { get; private set; }
    public decimal Total             { get; private set; }

    public decimal PendingToInvoice => OrderedQty - InvoicedQty;

    private PurchaseOrderLine() { }

    public static PurchaseOrderLine Create(
        Guid    subscriberId,
        Guid    purchaseOrderId,
        Guid    productId,
        string  description,
        decimal orderedQty,
        decimal unitCost,
        decimal vatPct,
        Guid    createdBy)
    {
        if (orderedQty <= 0)
            throw new ArgumentException("Ordered quantity must be greater than zero.", nameof(orderedQty));
        if (unitCost < 0)
            throw new ArgumentException("Unit cost cannot be negative.", nameof(unitCost));

        var subtotal  = orderedQty * unitCost;
        var taxAmount = subtotal * (vatPct / 100m);

        var d = new PurchaseOrderLine
        {
            Id              = Guid.NewGuid(),
            SubscriberId        = subscriberId,
            PurchaseOrderId = purchaseOrderId,
            ProductId       = productId,
            Description     = description.Trim(),
            OrderedQty      = orderedQty,
            InvoicedQty     = 0,
            UnitCost        = unitCost,
            Subtotal        = subtotal,
            TaxAmount       = taxAmount,
            Total           = subtotal + taxAmount,
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AddInvoicedQuantity(decimal quantity, Guid updatedBy)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        InvoicedQty += quantity;
        SetUpdated(updatedBy);
    }
}
