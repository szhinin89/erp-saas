using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// FLOW-READY-02F.1 — snapshot fiel de un impuesto SRI (&lt;impuesto&gt;) aplicado a una línea de
/// compra, tal como vino en el XML del proveedor (o, para líneas manuales, tal como se calculó desde
/// el catálogo SRI). Existe UNA fila por cada nodo &lt;impuesto&gt; de la línea de origen — incluye
/// también IVA e ICE (no solo IRBPNR), para preservar el detalle exacto del comprobante.
///
/// Estrategia de compatibilidad: esta colección es ADITIVA — nunca reemplaza ni recalcula los campos
/// escalares legacy de <see cref="PurchaseInvoiceDetail"/> (VatCode/VatRate/VatAmount, IceCode/
/// IceRate/IceAmount), que siguen siendo la fuente de <c>TaxInclusiveTotal</c>, los totales de la
/// factura y <c>PurchaseInvoiceTaxSummary</c>. IRBPNR es la única excepción: no tiene campos
/// escalares propios — <see cref="PurchaseInvoiceDetail.IrbpnrAmount"/> se deriva únicamente de esta
/// colección (nunca se trata como ICE: código, catálogo y resolución siempre separados).
/// </summary>
public sealed class PurchaseInvoiceDetailTax : IMustHaveTenant
{
    public const int TaxCodeMaxLen = 10;
    public const int TaxRateCodeMaxLen = 10;
    public const int TaxNameMaxLen = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseInvoiceDetailId { get; private set; }

    /// <summary>Código SRI &lt;impuesto&gt;/&lt;codigo&gt; — "2" IVA, "3" ICE, "5" IRBPNR.</summary>
    public string TaxCode { get; private set; } = null!;

    /// <summary>Código SRI &lt;codigoPorcentaje&gt; — identifica la tarifa específica dentro del catálogo del <see cref="TaxCode"/>.</summary>
    public string TaxRateCode { get; private set; } = null!;
    public string TaxName { get; private set; } = null!;
    public decimal? Rate { get; private set; }
    public SriTaxCalculationType CalculationType { get; private set; }
    public decimal TaxableBase { get; private set; }
    public decimal TaxAmount { get; private set; }
    public PurchaseTaxSource Source { get; private set; }

    private PurchaseInvoiceDetailTax() { }

    public static PurchaseInvoiceDetailTax Create(
        Guid purchaseInvoiceDetailId,
        Guid tenantId,
        string taxCode,
        string taxRateCode,
        string taxName,
        decimal? rate,
        SriTaxCalculationType calculationType,
        decimal taxableBase,
        decimal taxAmount,
        PurchaseTaxSource source
    )
    {
        if (purchaseInvoiceDetailId == Guid.Empty)
            throw new ArgumentException(
                "La línea de compra es obligatoria.",
                nameof(purchaseInvoiceDetailId)
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

        return new PurchaseInvoiceDetailTax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseInvoiceDetailId = purchaseInvoiceDetailId,
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
