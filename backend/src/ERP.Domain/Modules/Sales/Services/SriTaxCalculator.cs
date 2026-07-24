using static ERP.Domain.Common.FiscalPrecision;

namespace ERP.Domain.Modules.Sales.Services;

/// <summary>
/// Single source of truth for Ecuador SRI tax arithmetic.
/// Used by SalesInvoiceDetail.RecalcTaxes() and any read-model that needs the
/// same computation (e.g. invoice item search preview).
/// Formula: ICE = base × iceRate/100 ; VATbase = base + ICE ; VAT = VATbase × vatRate/100.
/// </summary>
public static class SriTaxCalculator
{
    public static (decimal IceAmount, decimal VatAmount, decimal TaxInclusive) Compute(
        decimal taxableBase, decimal vatRate, decimal iceRate)
    {
        var ice = iceRate > 0
            ? Math.Round(taxableBase * iceRate / 100m, TaxAmount, MidpointRounding.AwayFromZero)
            : 0m;
        var vatBase = taxableBase + ice;
        var vat = vatRate > 0
            ? Math.Round(vatBase * vatRate / 100m, TaxAmount, MidpointRounding.AwayFromZero)
            : 0m;
        return (ice, vat, Math.Round(taxableBase + ice + vat, TaxAmount, MidpointRounding.AwayFromZero));
    }
}
