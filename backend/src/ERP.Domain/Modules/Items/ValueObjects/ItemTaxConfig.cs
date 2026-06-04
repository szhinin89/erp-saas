namespace ERP.Domain.Modules.Items.ValueObjects;

public sealed class ItemTaxConfig
{
    public bool AppliesVatOnSale { get; private set; }
    public bool AppliesVatOnPurchase { get; private set; }
    public bool AppliesExciseTax { get; private set; }
    public string? SaleVatCode { get; private set; }
    public string? PurchaseVatCode { get; private set; }
    public string? ExciseTaxCode { get; private set; }
    public Guid? VatAccountId { get; private set; }
    public Guid? PurchaseVatAccountId { get; private set; }
    public Guid? ExciseAccountId { get; private set; }
    public string? SriServiceCode { get; private set; }

    private ItemTaxConfig() { }

    public static ItemTaxConfig Create(
        bool appliesVatOnSale,
        string? saleVatCode,
        Guid? vatAccountId,
        bool appliesVatOnPurchase,
        string? purchaseVatCode,
        Guid? purchaseVatAccountId,
        bool appliesExciseTax = false,
        string? exciseTaxCode = null,
        Guid? exciseAccountId = null,
        string? sriServiceCode = null)
    {
        if (appliesVatOnSale && string.IsNullOrWhiteSpace(saleVatCode))
            throw new ArgumentException("Debe especificar un código de IVA para venta.", nameof(saleVatCode));
        if (appliesVatOnPurchase && string.IsNullOrWhiteSpace(purchaseVatCode))
            throw new ArgumentException("Debe especificar un código de IVA para compra.", nameof(purchaseVatCode));
        if (appliesExciseTax && string.IsNullOrWhiteSpace(exciseTaxCode))
            throw new ArgumentException("Debe especificar un código de ICE.", nameof(exciseTaxCode));

        return new ItemTaxConfig
        {
            AppliesVatOnSale     = appliesVatOnSale,
            SaleVatCode          = saleVatCode?.Trim(),
            VatAccountId         = vatAccountId,
            AppliesVatOnPurchase = appliesVatOnPurchase,
            PurchaseVatCode      = purchaseVatCode?.Trim(),
            PurchaseVatAccountId = purchaseVatAccountId,
            AppliesExciseTax     = appliesExciseTax,
            ExciseTaxCode        = exciseTaxCode?.Trim(),
            ExciseAccountId      = exciseAccountId,
            SriServiceCode       = sriServiceCode?.Trim(),
        };
    }
}
