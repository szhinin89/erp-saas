namespace ERP.Domain.Modules.Accounting.Enums;

/// <summary>
/// Clasificación fija y universal de los componentes monetarios que un hecho contable puede
/// aportar a una línea de asiento (ADR-026 §4/§6.2, diseñado en Fase 3.5.1) — no es un catálogo
/// tenant-editable, mismo criterio ya usado para <see cref="AccountType"/>/<see cref="AccountNature"/>.
/// <see cref="Entities.PostingRuleLine.AmountKind"/> indica de qué monto de <c>PostingFact</c> se
/// toma el valor de esa línea; la resolución real (JournalFactory) pertenece a una fase posterior.
/// </summary>
public enum PostingAmountKind
{
    Subtotal,
    TaxVat,
    TaxIce,
    Discount,
    Retention,
    GrandTotal,

    // P0-02 Fase 6 (Remediación 01) — agregados al final, sin reasignar los valores subyacentes
    // ya existentes (persistidos como int, HasConversion<int>() en PostingRuleLineConfiguration)
    // — aditivo puro, exigido por el asiento compuesto de §19.1bis (PurchaseReturnAuthorized).
    AppliedToPayable,
    SupplierCredit,
    CostVarianceDebit,
    CostVarianceCredit,
    HistoricalCost,
}
