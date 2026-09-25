namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// ZH-DESIGN-SYSTEM-PRECISION-06 — escala CONTRACTUAL de los plazos de crédito. Fija del sistema
/// (no configurable por empresa) y única fuente: la usa la columna física
/// (<c>CreditInstallmentConfiguration</c>) y la expone <c>EffectivePrecisionPolicyDto</c> al frontend.
/// </summary>
public static class CreditTermsPrecision
{
    /// <summary>% de cada cuota sobre el total — <c>credit_installments.percentage numeric(5,2)</c>.</summary>
    public const int InstallmentPercentage = 2;
}
