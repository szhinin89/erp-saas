using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesNoteLine : AuditableEntity, ITenantEntity
{
    public const int DescriptionMaxLen = 300;
    public const int ProductCodeMaxLen = 100;
    public const int VatCodeMaxLen     = 2;

    public Guid    SalesNoteId  { get; private set; }
    public Guid    ProductId    { get; private set; }
    /// <summary>Snapshot del SaleCode del producto. Usado como &lt;codigoInterno&gt; en el XML SRI.</summary>
    public string  ProductCode  { get; private set; } = null!;
    public decimal Quantity     { get; private set; }
    public decimal UnitPrice    { get; private set; }
    public decimal Subtotal      { get; private set; }
    /// <summary>Código SRI del tipo de IVA: "0"=0%, "2"=12%, "3"=14%, "4"=15%, "5"=5%.</summary>
    public string  VatCode       { get; private set; } = "0";
    /// <summary>Porcentaje de IVA (ej: 15.00). Snapshot inmutable.</summary>
    public decimal VatPercentage { get; private set; }
    public decimal VatTotal      { get; private set; }
    public decimal Total         { get; private set; }
    public string  Description   { get; private set; } = null!;

    private SalesNoteLine() { }

    public static SalesNoteLine Create(
        Guid    tenantId,
        Guid    productId,
        string  productCode,
        decimal quantity,
        decimal unitPrice,
        string  vatCode,
        decimal vatPercentage,
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
            ProductCode  = string.IsNullOrWhiteSpace(productCode)
                               ? productId.ToString()[..8]
                               : productCode.Trim(),
            Quantity     = quantity,
            UnitPrice    = unitPrice,
            Subtotal     = subtotal,
            VatCode      = string.IsNullOrWhiteSpace(vatCode) ? "0" : vatCode.Trim(),
            VatPercentage = vatPercentage < 0 ? 0 : vatPercentage,
            VatTotal     = vatTotal,
            Total        = total,
            Description  = (description ?? string.Empty).Trim(),
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
