using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

/// <summary>
/// FLOW-READY-02F.1 — snapshot fiel de un nodo &lt;impuesto&gt; del XML, persistido junto con la
/// <see cref="PurchaseReceptionLine"/> de origen. Necesaria porque el flujo de Recepción Electrónica
/// es de 2 pasos (XML → línea de recepción persistida → más tarde "Crear compra" la lee) — sin esto,
/// un impuesto como IRBPNR (código "5") se perdería en el paso intermedio aunque el parser lo capture
/// correctamente. Solo guarda el dato crudo del XML (código/tarifa/base/valor) — la resolución contra
/// el catálogo SRI (nombre, tipo de cálculo) ocurre después, al crear el borrador de compra.
/// </summary>
public sealed class PurchaseReceptionLineTax : IMustHaveTenant
{
    public const int TaxCodeMaxLen = 10;
    public const int TaxRateCodeMaxLen = 10;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseReceptionLineId { get; private set; }

    /// <summary>Código SRI &lt;impuesto&gt;/&lt;codigo&gt; — "2" IVA, "3" ICE, "5" IRBPNR.</summary>
    public string TaxCode { get; private set; } = null!;

    /// <summary>Código SRI &lt;codigoPorcentaje&gt;.</summary>
    public string TaxRateCode { get; private set; } = null!;
    public decimal Tarifa { get; private set; }
    public decimal TaxableBase { get; private set; }
    public decimal TaxAmount { get; private set; }

    private PurchaseReceptionLineTax() { }

    public static PurchaseReceptionLineTax Create(
        Guid purchaseReceptionLineId,
        Guid tenantId,
        string taxCode,
        string taxRateCode,
        decimal tarifa,
        decimal taxableBase,
        decimal taxAmount
    )
    {
        if (purchaseReceptionLineId == Guid.Empty)
            throw new ArgumentException(
                "La línea de recepción es obligatoria.",
                nameof(purchaseReceptionLineId)
            );
        if (string.IsNullOrWhiteSpace(taxCode))
            throw new ArgumentException("El código de impuesto es obligatorio.", nameof(taxCode));
        if (string.IsNullOrWhiteSpace(taxRateCode))
            throw new ArgumentException(
                "El código de tarifa es obligatorio.",
                nameof(taxRateCode)
            );

        return new PurchaseReceptionLineTax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseReceptionLineId = purchaseReceptionLineId,
            TaxCode = taxCode.Trim(),
            TaxRateCode = taxRateCode.Trim(),
            Tarifa = tarifa,
            TaxableBase = taxableBase,
            TaxAmount = taxAmount,
        };
    }
}
