namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>Líneas de la factura de compra recibida.</summary>
public class PurchaseInvoiceDetail
{
    public Guid    Id          { get; set; }
    public Guid    InvoiceId   { get; set; }
    public Guid?   ProductId   { get; set; }
    public string  Description { get; set; } = null!;
    public decimal Qty         { get; set; }
    public decimal UnitPrice   { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal Subtotal    { get; set; }
    public string? VatCode     { get; set; }
    public decimal VatAmount   { get; set; }
    public decimal IceAmount   { get; set; }
    public decimal Total       { get; set; }
    public Guid?   WarehouseId { get; set; }
    public short   SortOrder   { get; set; }

    public PurchaseInvoice Invoice { get; set; } = null!;
}

/// <summary>
/// Alias legado para compatibilidad temporal. Use <see cref="PurchaseInvoiceDetail"/>.
/// </summary>
[Obsolete("Use PurchaseInvoiceDetail instead.")]
public class PurchInvDetail : PurchaseInvoiceDetail
{
}
