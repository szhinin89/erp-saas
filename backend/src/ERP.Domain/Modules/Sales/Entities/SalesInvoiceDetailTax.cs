using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — snapshot de un impuesto SRI aplicado a una línea de
/// venta (IVA/ICE/IRBPNR). Espejo de <see cref="ERP.Domain.Modules.Purchases.Entities.PurchaseInvoiceDetailTax"/>
/// para Ventas, pero con un rol distinto: es la fuente de verdad desde el día uno (no una colección
/// aditiva) — toda escritura de impuestos de línea en <see cref="SalesInvoiceDetail"/> pasa por aquí.
/// Los campos escalares legacy (IceCode/IceRate/IceAmount/SnapshotIceName) quedan como legacy
/// compatibility mirror, sincronizados desde esta colección — nunca al revés.
/// </summary>
public sealed class SalesInvoiceDetailTax : IMustHaveTenant
{
    public const int TaxCodeMaxLen = 10;
    public const int TaxRateCodeMaxLen = 10;
    public const int TaxNameMaxLen = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SalesInvoiceDetailId { get; private set; }

    /// <summary>Código SRI &lt;impuesto&gt;/&lt;codigo&gt; — "2" IVA, "3" ICE, "5" IRBPNR.</summary>
    public string TaxCode { get; private set; } = null!;

    /// <summary>Código SRI &lt;codigoPorcentaje&gt; — identifica la tarifa específica dentro del catálogo del <see cref="TaxCode"/>.</summary>
    public string TaxRateCode { get; private set; } = null!;
    public string TaxName { get; private set; } = null!;
    public decimal? Rate { get; private set; }
    public SriTaxCalculationType CalculationType { get; private set; }
    public decimal TaxableBase { get; private set; }
    public decimal TaxAmount { get; private set; }
    public SalesTaxSource Source { get; private set; }

    private SalesInvoiceDetailTax() { }

    public static SalesInvoiceDetailTax Create(
        Guid salesInvoiceDetailId,
        Guid tenantId,
        string taxCode,
        string taxRateCode,
        string taxName,
        decimal? rate,
        SriTaxCalculationType calculationType,
        decimal taxableBase,
        decimal taxAmount,
        SalesTaxSource source
    )
    {
        if (salesInvoiceDetailId == Guid.Empty)
            throw new ArgumentException(
                "La línea de venta es obligatoria.",
                nameof(salesInvoiceDetailId)
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

        return new SalesInvoiceDetailTax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SalesInvoiceDetailId = salesInvoiceDetailId,
            TaxCode = taxCode.Trim(),
            TaxRateCode = taxRateCode.Trim(),
            TaxName = taxName.Trim(),
            Rate = rate,
            CalculationType = calculationType,
            TaxableBase = taxableBase,
            TaxAmount = taxAmount,
            Source = source,
        };
    }
}
