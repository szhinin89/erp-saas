using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-3) — impuesto (IVA/ICE/IRBPNR) de una línea
/// de <see cref="SalesReturnDetail"/>, prorrateado desde <see cref="SalesInvoiceDetailTax"/> de la
/// línea de factura original por la fracción <c>Quantity / originalLine.Quantity</c> (mismo patrón
/// que <see cref="ERP.Domain.Modules.Purchases.Entities.PurchaseReturnDetailTax"/> en Compras). Nunca
/// se recalcula desde la configuración tributaria actual del ítem. Sin <c>Source</c>: siempre es una
/// proporción del snapshot fiscal ya congelado en la factura, nunca XML ni cálculo independiente.
/// </summary>
public sealed class SalesReturnDetailTax : IMustHaveTenant
{
    public const int TaxCodeMaxLen = 10;
    public const int TaxRateCodeMaxLen = 10;
    public const int TaxNameMaxLen = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SalesReturnDetailId { get; private set; }

    /// <summary>Código SRI &lt;impuesto&gt;/&lt;codigo&gt; — "2" IVA, "3" ICE, "5" IRBPNR.</summary>
    public string TaxCode { get; private set; } = null!;
    public string TaxRateCode { get; private set; } = null!;
    public string TaxName { get; private set; } = null!;
    public decimal? Rate { get; private set; }
    public SriTaxCalculationType CalculationType { get; private set; }
    public decimal TaxAmount { get; private set; }

    private SalesReturnDetailTax() { }

    public static SalesReturnDetailTax Create(
        Guid salesReturnDetailId,
        Guid tenantId,
        string taxCode,
        string taxRateCode,
        string taxName,
        decimal? rate,
        SriTaxCalculationType calculationType,
        decimal taxAmount
    )
    {
        if (salesReturnDetailId == Guid.Empty)
            throw new ArgumentException(
                "La línea de devolución es obligatoria.",
                nameof(salesReturnDetailId)
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

        return new SalesReturnDetailTax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SalesReturnDetailId = salesReturnDetailId,
            TaxCode = taxCode.Trim(),
            TaxRateCode = taxRateCode.Trim(),
            TaxName = taxName.Trim(),
            Rate = rate,
            CalculationType = calculationType,
            TaxAmount = taxAmount,
        };
    }
}
