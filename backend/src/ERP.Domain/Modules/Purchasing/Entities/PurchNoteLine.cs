using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchNoteLine : AuditableEntity, ISubscriberScopedEntity
{
    public const int DescriptionMaxLen         = 300;
    public const int SupplierProductCodeMaxLen = 50;

    public Guid    PurchNoteId          { get; private set; }
    public Guid?   ProductId            { get; private set; }
    public string? SupplierProductCode  { get; private set; }
    public string  Description          { get; private set; } = null!;
    public decimal Quantity             { get; private set; }
    public decimal UnitPrice            { get; private set; }
    public decimal Subtotal             { get; private set; }
    public decimal VatAmount            { get; private set; }
    public decimal Total                { get; private set; }

    private PurchNoteLine() { }

    public static PurchNoteLine Create(
        Guid    subscriberId,
        Guid?   productId,
        string? supplierProductCode,
        string  description,
        decimal quantity,
        decimal unitPrice,
        decimal subtotal,
        decimal vatAmount,
        decimal total,
        Guid    createdBy)
    {
        var d = new PurchNoteLine
        {
            Id                  = Guid.NewGuid(),
            SubscriberId            = subscriberId,
            PurchNoteId         = Guid.Empty,
            ProductId           = productId,
            SupplierProductCode = string.IsNullOrWhiteSpace(supplierProductCode) ? null : supplierProductCode.Trim(),
            Description         = (description ?? string.Empty).Trim(),
            Quantity            = quantity,
            UnitPrice           = unitPrice,
            Subtotal            = subtotal,
            VatAmount           = vatAmount,
            Total               = total,
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AssignNoteId(Guid noteId)
    {
        if (PurchNoteId != Guid.Empty)
            throw new InvalidOperationException("Line is already assigned to a note.");
        PurchNoteId = noteId;
    }
}
