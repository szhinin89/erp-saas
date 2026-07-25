namespace ERP.Domain.Modules.Accounting.Enums;

/// <summary>
/// Clasificación contable universal de partida doble (ADR-026 §5) — no es un catálogo
/// tenant-editable, a diferencia de ItemTypeDefinition. Extensión futura únicamente como
/// valor nuevo dentro de este mismo conjunto, nunca como lógica condicional por país/tenant.
/// </summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense
}
