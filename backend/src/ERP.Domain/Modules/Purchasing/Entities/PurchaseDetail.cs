using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseDetail : AuditableEntity, ITenantEntity
{
    public Guid   PurchaseDocumentId { get; private set; }
    public Guid?  ProductId          { get; private set; }
    public string Description        { get; private set; } = null!;
    public decimal Quantity          { get; private set; }
    public decimal QtyReceived       { get; private set; }
    public decimal UnitPrice         { get; private set; }
    public decimal DiscountPct       { get; private set; }
    public string  VatCode           { get; private set; } = "0";
    public decimal VatPercentage     { get; private set; }
    public decimal Subtotal          { get; private set; }
    public decimal VatAmount         { get; private set; }
    public decimal Total             { get; private set; }

    private PurchaseDetail() { }

    public static PurchaseDetail FromBillLine(PurchBillLine line)
    {
        var d = new PurchaseDetail
        {
            Id                 = line.Id,
            TenantId           = line.TenantId,
            PurchaseDocumentId = line.PurchBillId,
            ProductId          = line.ProductId,
            Description        = line.Description,
            Quantity           = line.Quantity,
            UnitPrice          = line.UnitPrice,
            DiscountPct        = line.DiscountPct,
            VatPercentage      = line.VatPct,
            Subtotal           = line.Subtotal,
            VatAmount          = line.VatAmount,
            Total              = line.Total,
            CreatedAt          = line.CreatedAt,
            UpdatedAt          = line.UpdatedAt,
            CreatedBy          = line.CreatedBy,
            UpdatedBy          = line.UpdatedBy,
        };
        return d;
    }

    public static PurchaseDetail FromNoteLine(PurchNoteLine line)
    {
        var d = new PurchaseDetail
        {
            Id                 = line.Id,
            TenantId           = line.TenantId,
            PurchaseDocumentId = line.PurchNoteId,
            ProductId          = line.ProductId,
            Description        = line.Description,
            Quantity           = line.Quantity,
            UnitPrice          = line.UnitPrice,
            Subtotal           = line.Subtotal,
            VatAmount          = line.VatAmount,
            Total              = line.Total,
            CreatedAt          = line.CreatedAt,
            UpdatedAt          = line.UpdatedAt,
            CreatedBy          = line.CreatedBy,
            UpdatedBy          = line.UpdatedBy,
        };
        return d;
    }
}
