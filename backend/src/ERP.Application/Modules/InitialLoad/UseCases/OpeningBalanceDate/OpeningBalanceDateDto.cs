namespace ERP.Application.Modules.InitialLoad.UseCases.OpeningBalanceDate;

/// <summary>
/// Fecha de apertura de saldos de la empresa (<c>Company.OpeningBalanceDate</c>) y si puede
/// definirse/corregirse. Con <see cref="ConfirmedOpeningDates"/> no vacío solo se admite esa fecha.
/// </summary>
public sealed record OpeningBalanceDateDto(
    DateOnly? OpeningBalanceDate,
    bool HasRealOperations,
    IReadOnlyList<DateOnly> ConfirmedOpeningDates,
    bool IsLocked,
    string? LockReason
);
