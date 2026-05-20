using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesDetail : AuditableEntity, ITenantEntity
{
    public const int DescriptionMaxLen = 500;
    public const int ProductCodeMaxLen = 100;
    public const int VatCodeMaxLen     = 5;

    public Guid   SalesDocumentId { get; private set; }
    public Guid?  ProductId       { get; private set; }
    public string ProductCode     { get; private set; } = "";
    public string Description     { get; private set; } = null!;
    public string? UnitCode       { get; private set; }
    public decimal Quantity      { get; private set; }
    public decimal UnitPrice     { get; private set; }
    public decimal DiscountPct   { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string  VatCode       { get; private set; } = "0";
    public decimal VatPercentage { get; private set; }
    public decimal Subtotal      { get; private set; }
    public decimal VatAmount     { get; private set; }
    public decimal IceAmount     { get; private set; }
    public decimal Total         { get; private set; }
    public short   SortOrder     { get; private set; }

    private SalesDetail() { }

    public static SalesDetail Create(
        Guid tenantId,
        Guid productId,
        string productCode,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        string vatCode,
        decimal vatPercentage,
        decimal vatAmount,
        string description,
        Guid createdBy)
    {
        var subtotal = quantity * unitPrice - discountAmount;
        var line = new SalesDetail
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            SalesDocumentId = Guid.Empty,
            ProductId       = productId,
            ProductCode     = string.IsNullOrWhiteSpace(productCode) ? productId.ToString()[..8] : productCode.Trim(),
            Quantity        = quantity,
            UnitPrice       = unitPrice,
            DiscountAmount  = discountAmount < 0 ? 0 : discountAmount,
            Subtotal        = subtotal < 0 ? 0 : subtotal,
            VatCode         = string.IsNullOrWhiteSpace(vatCode) ? "0" : vatCode.Trim(),
            VatPercentage   = vatPercentage < 0 ? 0 : vatPercentage,
            VatAmount       = vatAmount,
            Total           = subtotal + vatAmount,
            Description     = description.Trim(),
        };
        line.SetCreated(createdBy);
        return line;
    }

    public void AssignDocumentId(Guid salesDocumentId)
    {
        if (SalesDocumentId != Guid.Empty)
            throw new InvalidOperationException("Line is already assigned to a document.");
        SalesDocumentId = salesDocumentId;
    }

    public static SalesDetail FromNoteLine(SalesNoteLine line)
    {
        var d = new SalesDetail
        {
            Id              = line.Id,
            TenantId        = line.TenantId,
            SalesDocumentId = line.SalesNoteId,
            ProductId       = line.ProductId,
            ProductCode     = line.ProductCode,
            Description     = line.Description,
            Quantity        = line.Quantity,
            UnitPrice       = line.UnitPrice,
            VatCode         = line.VatCode,
            VatPercentage   = line.VatPercentage,
            Subtotal        = line.Subtotal,
            VatAmount       = line.VatTotal,
            Total           = line.Total,
            CreatedAt       = line.CreatedAt,
            UpdatedAt       = line.UpdatedAt,
            CreatedBy       = line.CreatedBy,
            UpdatedBy       = line.UpdatedBy,
        };
        return d;
    }
}
