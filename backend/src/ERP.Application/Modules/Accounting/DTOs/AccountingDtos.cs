namespace ERP.Application.Modules.Accounting.DTOs;

public sealed record AccountDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentAccountId,
    string AccountType,
    string Nature,
    bool AllowsPosting,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AccountingPeriodDto(
    Guid Id,
    int FiscalYear,
    int PeriodNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateTime? ClosedAtUtc,
    Guid? ClosedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PostingRuleDto(
    Guid Id,
    string SourceModule,
    string FactType,
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    string? TaxCode,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
