using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.ValueObjects;

public sealed class ItemTaxConfig
{
    public string? SaleVatCode { get; private set; }
    public string? PurchaseVatCode { get; private set; }
    public string? ExciseTaxCode { get; private set; }

    private ItemTaxConfig() { }

    public static ItemTaxConfig Create(
        string? saleVatCode,
        string? purchaseVatCode,
        string? exciseTaxCode = null)
    {
        return new ItemTaxConfig
        {
            SaleVatCode = OptionalCode.Normalize(saleVatCode),
            PurchaseVatCode = OptionalCode.Normalize(purchaseVatCode),
            ExciseTaxCode = OptionalCode.Normalize(exciseTaxCode),
        };
    }
}
