using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesNoteLine : AuditableEntity, ITenantEntity
{
    public const int DescriptionMaxLen = 300;
    public const int ProductCodeMaxLen = 100;

    public Guid    SalesNoteId  { get; private set; }
    public Guid    ProductId    { get; private set; }
    /// <summary>Snapshot del SaleCode del producto. Usado como &lt;codigoInterno&gt; en el XML SRI.</summary>
    public string  ProductCode  { get; private set; } = null!;
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
        string  productCode,
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
            ProductCode = string.IsNullOrWhiteSpace(productCode)
                              ? productId.ToString()[..8]
                              : productCode.Trim(),
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
