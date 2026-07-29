using static ERP.Domain.Common.FiscalPrecision;

namespace ERP.Domain.Modules.Purchases.Services;

/// <summary>
/// Motor de cálculo de retenciones Ecuador SRI — Domain Service puro.
/// Sin dependencias externas. Recibe datos resueltos y devuelve resultado.
///
/// Reglas fiscales Ecuador:
/// - Si proveedor IsRetentionExempt → no retener (RISE, microempresa)
/// - Retención IVA: se aplica sobre TotalVat de la compra
/// - Retención Renta: se aplica sobre base imponible (subtotal - descuento)
/// - La tasa depende del código SRI configurado en el proveedor
/// </summary>
public static class RetentionCalculator
{
    /// <summary>
    /// Calcula las líneas de retención para una compra.
    /// </summary>
    /// <param name="totalVat">Total IVA de la compra (base para retención IVA)</param>
    /// <param name="taxableBaseIncome">Base imponible para retención renta (subtotal - descuento + ICE)</param>
    /// <param name="isRetentionExempt">Proveedor exento de retención</param>
    /// <param name="vatRetentionCode">Código retención IVA del proveedor (ej: "725")</param>
    /// <param name="vatRetentionPct">Porcentaje de retención IVA (ej: 30)</param>
    /// <param name="vatRetentionName">Descripción del código IVA (snapshot)</param>
    /// <param name="incomeRetentionCode">Código retención Renta del proveedor (ej: "303")</param>
    /// <param name="incomeRetentionPct">Porcentaje de retención Renta (ej: 10)</param>
    /// <param name="incomeRetentionName">Descripción del código Renta (snapshot)</param>
    public static RetentionCalculationResult Calculate(
        decimal totalVat,
        decimal taxableBaseIncome,
        bool isRetentionExempt,
        string? vatRetentionCode,
        decimal vatRetentionPct,
        string? vatRetentionName,
        string? incomeRetentionCode,
        decimal incomeRetentionPct,
        string? incomeRetentionName
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

        // ── Retención IVA ───────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(vatRetentionCode) && vatRetentionPct > 0 && totalVat > 0)
        {
            var amount = Math.Round(
                totalVat * vatRetentionPct / 100m,
                TaxAmount,
                MidpointRounding.AwayFromZero
            );
            lines.Add(
                new RetentionLineResult(
                    "IVA",
                    vatRetentionCode,
                    vatRetentionName ?? vatRetentionCode,
                    totalVat,
                    vatRetentionPct,
                    amount
                )
            );
        }

        // ── Retención Renta ─────────────────────────────────────────────
        if (
            !string.IsNullOrWhiteSpace(incomeRetentionCode)
            && incomeRetentionPct > 0
            && taxableBaseIncome > 0
        )
        {
            var amount = Math.Round(
                taxableBaseIncome * incomeRetentionPct / 100m,
                TaxAmount,
                MidpointRounding.AwayFromZero
            );
            lines.Add(
                new RetentionLineResult(
                    "RENTA",
                    incomeRetentionCode,
                    incomeRetentionName ?? incomeRetentionCode,
                    taxableBaseIncome,
                    incomeRetentionPct,
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
