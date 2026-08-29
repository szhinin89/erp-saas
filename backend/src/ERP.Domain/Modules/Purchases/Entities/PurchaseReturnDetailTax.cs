using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-1) — snapshot de un impuesto SRI
/// (IVA/ICE/IRBPNR) de una línea de devolución de compra, prorrateado desde
/// <see cref="PurchaseInvoiceDetailTax"/> de la línea de factura original (fracción
/// <c>PurchaseReturnDetail.Quantity / originalLine.Quantity</c>, misma fórmula que ya rige
/// <c>ReturnedVatAmount</c>/<c>ReturnedIceAmount</c> — ver <c>PurchaseReturn.Authorize</c>). Nunca se
/// recalcula desde la configuración tributaria actual del producto: es una copia proporcional del
/// snapshot fiscal ya congelado en la factura, igual que el resto del snapshot financiero de esta
/// línea (congelado una única vez en <see cref="PurchaseReturn.Authorize"/>).
/// </summary>
public sealed class PurchaseReturnDetailTax : IMustHaveTenant
{
    public const int TaxCodeMaxLen = 10;
    public const int TaxRateCodeMaxLen = 10;
    public const int TaxNameMaxLen = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseReturnDetailId { get; private set; }

    /// <summary>Código SRI &lt;impuesto&gt;/&lt;codigo&gt; — "2" IVA, "3" ICE, "5" IRBPNR.</summary>
    public string TaxCode { get; private set; } = null!;
    public string TaxRateCode { get; private set; } = null!;
    public string TaxName { get; private set; } = null!;
    public decimal? Rate { get; private set; }
    public SriTaxCalculationType CalculationType { get; private set; }
    public decimal TaxableBase { get; private set; }
    public decimal TaxAmount { get; private set; }

    private PurchaseReturnDetailTax() { }

    public static PurchaseReturnDetailTax Create(
        Guid purchaseReturnDetailId,
        Guid tenantId,
        string taxCode,
        string taxRateCode,
        string taxName,
        decimal? rate,
        SriTaxCalculationType calculationType,
        decimal taxableBase,
        decimal taxAmount
    )
    {
        if (purchaseReturnDetailId == Guid.Empty)
            throw new ArgumentException(
                "La línea de devolución es obligatoria.",
                nameof(purchaseReturnDetailId)
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

        return new PurchaseReturnDetailTax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseReturnDetailId = purchaseReturnDetailId,
            TaxCode = taxCode.Trim(),
            TaxRateCode = taxRateCode.Trim(),
            TaxName = taxName.Trim(),
            Rate = rate,
            CalculationType = calculationType,
            TaxableBase = taxableBase,
            TaxAmount = taxAmount,
        };
    }
}
