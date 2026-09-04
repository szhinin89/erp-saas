namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// RETENTIONS-RIDE-TEMPLATE-03C — una línea <c>&lt;impuesto&gt;</c> del comprobante de retención
/// autorizado. No reutiliza <see cref="RideTaxSummary"/>: <see cref="RetentionCode"/>
/// (<c>codigoRetencion</c>, p.ej. "303"/"725") es semánticamente un código de retención, no un
/// "código de porcentaje" (<c>TaxPercentageCode</c> en <c>RideTaxSummary</c>) — forzar ese campo
/// habría mezclado dos conceptos del catálogo SRI que son distintos (Tabla 21 vs. tarifa/código de
/// porcentaje de Factura).
/// </summary>
public sealed record RetentionRideTaxLine
{
    public string TaxCode { get; }
    public string RetentionCode { get; }
    public decimal BaseAmount { get; }
    public decimal RetentionRate { get; }
    public decimal RetainedAmount { get; }

    private RetentionRideTaxLine(
        string taxCode,
        string retentionCode,
        decimal baseAmount,
        decimal retentionRate,
        decimal retainedAmount
    )
    {
        TaxCode = taxCode;
        RetentionCode = retentionCode;
        BaseAmount = baseAmount;
        RetentionRate = retentionRate;
        RetainedAmount = retainedAmount;
    }

    public static RetentionRideTaxLine Create(
        string taxCode,
        string retentionCode,
        decimal baseAmount,
        decimal retentionRate,
        decimal retainedAmount
    )
    {
        if (string.IsNullOrWhiteSpace(taxCode))
            throw new ArgumentException("El código de impuesto es obligatorio.", nameof(taxCode));
        if (string.IsNullOrWhiteSpace(retentionCode))
            throw new ArgumentException(
                "El código de retención es obligatorio.",
                nameof(retentionCode)
            );
        if (baseAmount < 0)
            throw new ArgumentException(
                "La base imponible no puede ser negativa.",
                nameof(baseAmount)
            );
        if (retentionRate < 0)
            throw new ArgumentException(
                "El porcentaje de retención no puede ser negativo.",
                nameof(retentionRate)
            );
        if (retainedAmount < 0)
            throw new ArgumentException(
                "El valor retenido no puede ser negativo.",
                nameof(retainedAmount)
            );

        return new RetentionRideTaxLine(
            taxCode.Trim(),
            retentionCode.Trim(),
            baseAmount,
            retentionRate,
            retainedAmount
        );
    }
}
