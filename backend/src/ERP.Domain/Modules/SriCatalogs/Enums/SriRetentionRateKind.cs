namespace ERP.Domain.Modules.SriCatalogs.Enums;

/// <summary>
/// ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 (ADR-037 D7) — forma en que la fuente oficial fija la tarifa de una
/// versión de retención. <see cref="Fixed"/>: una sola tarifa (<c>Percentage</c>; null solo en versiones
/// heredadas no verificadas, sin coincidencia exigible). <see cref="Conditional"/>: la fuente no fija una
/// tarifa única ("12 o 14", "1 /0 según resolución…"): nunca se guarda un porcentaje y el ERP no puede
/// resolverla por sí mismo — la resolución falla cerrado.
/// </summary>
public enum SriRetentionRateKind
{
    Fixed = 1,
    Conditional = 2,
}
