namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// Una cifra de impuesto tal como aparece en el XML autorizado. Se reutiliza para dos niveles
/// que en el esquema SRI son estructuralmente casi idénticos: el resumen de impuestos del
/// documento (<c>totalImpuesto</c>, sin tarifa) y el impuesto de cada línea de detalle
/// (<c>impuesto</c>, con tarifa) — de ahí que <see cref="Rate"/> sea opcional en vez de crear
/// un segundo VO fuera del catálogo congelado en ADR-025 §5.
/// </summary>
public sealed record RideTaxSummary
{
    public string TaxCode { get; }
    public string TaxPercentageCode { get; }
    public decimal? Rate { get; }
    public decimal TaxableBase { get; }
    public decimal TaxAmount { get; }

    private RideTaxSummary(string taxCode, string taxPercentageCode, decimal? rate, decimal taxableBase, decimal taxAmount)
    {
        TaxCode = taxCode;
        TaxPercentageCode = taxPercentageCode;
        Rate = rate;
        TaxableBase = taxableBase;
        TaxAmount = taxAmount;
    }

    public static RideTaxSummary Create(string taxCode, string taxPercentageCode, decimal taxableBase, decimal taxAmount, decimal? rate = null)
    {
        if (string.IsNullOrWhiteSpace(taxCode))
            throw new ArgumentException("El código de impuesto es obligatorio.", nameof(taxCode));
        if (string.IsNullOrWhiteSpace(taxPercentageCode))
            throw new ArgumentException("El código de porcentaje es obligatorio.", nameof(taxPercentageCode));
        if (taxableBase < 0)
            throw new ArgumentException("La base imponible no puede ser negativa.", nameof(taxableBase));
        if (taxAmount < 0)
            throw new ArgumentException("El valor del impuesto no puede ser negativo.", nameof(taxAmount));
        if (rate is < 0)
            throw new ArgumentException("La tarifa no puede ser negativa.", nameof(rate));

        return new RideTaxSummary(taxCode.Trim(), taxPercentageCode.Trim(), rate, taxableBase, taxAmount);
    }
}
