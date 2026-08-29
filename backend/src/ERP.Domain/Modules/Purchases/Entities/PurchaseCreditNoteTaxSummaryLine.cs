using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2 — corrección post-revisión) — impuesto
/// (IVA/ICE/IRBPNR/futuro) de una <see cref="PurchaseCreditNoteTaxSummary"/>, espejo del mismo patrón
/// que <see cref="PurchaseInvoiceDetailTax"/>/<see cref="PurchaseReturnDetailTax"/>: una fila por
/// impuesto real, en vez de una columna fija por impuesto. Reemplaza el diseño inicial de esta
/// subfase (IrbpnrCode/IrbpnrRate/IrbpnrName/IrbpnrAmount como columnas nuevas en
/// <see cref="PurchaseCreditNoteTaxSummary"/>) — descartado por reproducir el mismo antipatrón que el
/// ADR ya señala en los catálogos SRI y en <c>ItemTaxConfig</c>. No tiene <c>Source</c> (siempre es
/// heredado/prorrateado del resumen fiscal de origen, nunca XML ni cálculo independiente).
/// </summary>
public sealed class PurchaseCreditNoteTaxSummaryLine : IMustHaveTenant
{
    public const int TaxCodeMaxLen = 10;
    public const int TaxRateCodeMaxLen = 10;
    public const int TaxNameMaxLen = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseCreditNoteTaxSummaryId { get; private set; }

    /// <summary>Código SRI &lt;impuesto&gt;/&lt;codigo&gt; — "2" IVA, "3" ICE, "5" IRBPNR.</summary>
    public string TaxCode { get; private set; } = null!;
    public string TaxRateCode { get; private set; } = null!;
    public string TaxName { get; private set; } = null!;
    public decimal? Rate { get; private set; }
    public SriTaxCalculationType CalculationType { get; private set; }
    public decimal TaxAmount { get; private set; }

    private PurchaseCreditNoteTaxSummaryLine() { }

    internal static PurchaseCreditNoteTaxSummaryLine Create(
        Guid purchaseCreditNoteTaxSummaryId,
        Guid tenantId,
        string taxCode,
        string taxRateCode,
        string taxName,
        decimal? rate,
        SriTaxCalculationType calculationType,
        decimal taxAmount
    )
    {
        if (purchaseCreditNoteTaxSummaryId == Guid.Empty)
            throw new ArgumentException(
                "El resumen fiscal de la nota de crédito es obligatorio.",
                nameof(purchaseCreditNoteTaxSummaryId)
            );
        if (string.IsNullOrWhiteSpace(taxCode))
            throw new ArgumentException("El código de impuesto es obligatorio.", nameof(taxCode));
        if (string.IsNullOrWhiteSpace(taxRateCode))
            throw new ArgumentException(
                "El código de tarifa es obligatorio.",
                nameof(taxRateCode)
            );
        if (string.IsNullOrWhiteSpace(taxName))
            throw new ArgumentException("El nombre del impuesto es obligatorio.", nameof(taxName));
        if (taxAmount < 0)
            throw new ArgumentException(
                "El monto del impuesto no puede ser negativo.",
                nameof(taxAmount)
            );

        return new PurchaseCreditNoteTaxSummaryLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseCreditNoteTaxSummaryId = purchaseCreditNoteTaxSummaryId,
            TaxCode = taxCode.Trim(),
            TaxRateCode = taxRateCode.Trim(),
            TaxName = taxName.Trim(),
            Rate = rate,
            CalculationType = calculationType,
            TaxAmount = taxAmount,
        };
    }
}
