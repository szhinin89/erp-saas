using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesNoteLine : AuditableEntity, ITenantEntity
{
    public const int DescriptionMaxLen = 300;

    public Guid    SalesNoteId  { get; private set; }
    public Guid    ProductId    { get; private set; }
    public decimal Quantity     { get; private set; }
    public decimal UnitPrice    { get; private set; }
    public decimal Subtotal     { get; private set; }
    public decimal VatTotal     { get; private set; }
    public decimal Total        { get; private set; }
    public string  Description  { get; private set; } = null!;

    private SalesNoteLine() { }

    public static SalesNoteLine Create(
        Guid    tenantId,
        Guid    productId,
        decimal quantity,
        decimal unitPrice,
        decimal vatTotal,
        string  description,
        Guid    createdBy)
    {
        var subtotal = quantity * unitPrice;
        var total    = subtotal + vatTotal;

        var d = new SalesNoteLine
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            SalesNoteId = Guid.Empty,
            ProductId   = productId,
            Quantity    = quantity,
            UnitPrice   = unitPrice,
            Subtotal    = subtotal,
            VatTotal    = vatTotal,
            Total       = total,
            Description = (description ?? string.Empty).Trim(),
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AssignNoteId(Guid noteId)
    {
        if (SalesNoteId != Guid.Empty)
            throw new InvalidOperationException("Line is already assigned to a note.");
        SalesNoteId = noteId;
    }
}
