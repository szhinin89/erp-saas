namespace ERP.Domain.Modules.Purchases.Services;

public sealed record RetentionLineResult(
    string  TaxType,
    string  RetentionCode,
    string  RetentionCodeName,
    decimal TaxableBase,
    decimal RetentionPct,
    decimal AmountRetained);

public sealed record RetentionCalculationResult(
    IReadOnlyList<RetentionLineResult> Lines,
    decimal TotalRetainedVat,
    decimal TotalRetainedIncome,
    decimal TotalRetainedIsd,
    decimal TotalRetained,
    string? SkipReason);
