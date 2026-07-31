namespace ERP.Domain.Common;

/// <summary>
/// Single source of truth for Ecuador SRI tax arithmetic.
/// Used by SalesInvoiceDetail.RecalcTaxes() and PurchaseInvoiceDetail.RecalcTaxes(), and any
/// read-model that needs the same computation (e.g. invoice item search preview). Lives in
/// Domain.Common (alongside FiscalPrecision) because it is a cross-module authority shared by
/// Sales and Purchases — neither module owns it.
/// Formula: ICE = base × iceRate/100 ; VATbase = base + ICE ; VAT = VATbase × vatRate/100.
/// </summary>
public static class SriTaxCalculator
{
    public static (decimal IceAmount, decimal VatAmount, decimal TaxInclusive) Compute(
        decimal taxableBase,
        decimal vatRate,
        decimal iceRate
    )
    {
        var ice =
            iceRate > 0
                ? Math.Round(
                    taxableBase * iceRate / 100m,
                    FiscalPrecision.TaxAmount,
                    MidpointRounding.AwayFromZero
                )
                : 0m;
        var vatBase = taxableBase + ice;
        var vat =
            vatRate > 0
                ? Math.Round(
                    vatBase * vatRate / 100m,
                    FiscalPrecision.TaxAmount,
                    MidpointRounding.AwayFromZero
                )
                : 0m;
        return (
            ice,
            vat,
            Math.Round(
                taxableBase + ice + vat,
                FiscalPrecision.TaxAmount,
                MidpointRounding.AwayFromZero
            )
        );
    }
}
