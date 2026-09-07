using static ERP.Domain.Common.FiscalPrecision;

namespace ERP.Domain.Modules.Purchases.Services;

/// <summary>
/// Insumo resuelto de un código de retención candidato (ya validado contra catálogo SRI activo)
/// para calcular una línea. RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01: reemplaza los antiguos
/// parámetros fijos "un código IVA + un código Renta" por una colección — un proveedor puede tener
/// N candidatos activos por impuesto.
/// </summary>
public sealed record RetentionCandidate(
    string TaxType,
    string RetentionCode,
    decimal RetentionPct,
    string? RetentionCodeName
);

/// <summary>
/// Motor de cálculo de retenciones Ecuador SRI — Domain Service puro.
/// Sin dependencias externas. Recibe datos resueltos y devuelve resultado.
///
/// Reglas fiscales Ecuador:
/// - Si proveedor IsRetentionExempt → no retener (RISE, microempresa)
/// - Retención IVA: se aplica sobre TotalVat de la compra, por cada candidato IVA
/// - Retención Renta: se aplica sobre base imponible (subtotal - descuento), por cada candidato Renta
/// - La tasa depende del código SRI de cada candidato configurado en el proveedor
/// </summary>
public static class RetentionCalculator
{
    /// <summary>
    /// Calcula las líneas de retención para una compra, una por cada candidato activo elegible
    /// (0..N por impuesto — ya no está limitado a máximo un código IVA + un código Renta).
    /// </summary>
    /// <param name="totalVat">Total IVA de la compra (base para retención IVA)</param>
    /// <param name="taxableBaseIncome">Base imponible para retención renta (subtotal - descuento + ICE)</param>
    /// <param name="isRetentionExempt">Proveedor exento de retención</param>
    /// <param name="candidates">Códigos de retención candidatos del proveedor, ya resueltos contra catálogo</param>
    public static RetentionCalculationResult Calculate(
        decimal totalVat,
        decimal taxableBaseIncome,
        bool isRetentionExempt,
        IReadOnlyList<RetentionCandidate> candidates
    )
    {
        if (isRetentionExempt)
            return new RetentionCalculationResult(
                Array.Empty<RetentionLineResult>(),
                0,
                0,
                0,
                0,
                "Proveedor exento de retención (RISE / microempresa / sector público)."
            );

        var lines = new List<RetentionLineResult>();

        foreach (var candidate in candidates)
        {
            var isVat = string.Equals(candidate.TaxType, "IVA", StringComparison.OrdinalIgnoreCase);
            var baseAmount = isVat ? totalVat : taxableBaseIncome;

            if (
                string.IsNullOrWhiteSpace(candidate.RetentionCode)
                || candidate.RetentionPct <= 0
                || baseAmount <= 0
            )
                continue;

            var amount = Math.Round(
                baseAmount * candidate.RetentionPct / 100m,
                TaxAmount,
                MidpointRounding.AwayFromZero
            );
            lines.Add(
                new RetentionLineResult(
                    isVat ? "IVA" : "RENTA",
                    candidate.RetentionCode,
                    candidate.RetentionCodeName ?? candidate.RetentionCode,
                    baseAmount,
                    candidate.RetentionPct,
                    amount
                )
            );
        }

        var totalRetVat = lines.Where(l => l.TaxType == "IVA").Sum(l => l.AmountRetained);
        var totalRetIncome = lines.Where(l => l.TaxType == "RENTA").Sum(l => l.AmountRetained);

        return new RetentionCalculationResult(
            lines,
            totalRetVat,
            totalRetIncome,
            0,
            totalRetVat + totalRetIncome,
            lines.Count == 0 ? "Sin códigos de retención configurados en el proveedor." : null
        );
    }
}
