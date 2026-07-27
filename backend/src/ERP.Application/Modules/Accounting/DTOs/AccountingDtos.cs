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
    IReadOnlyList<PostingRuleLineDto> Lines,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Fase 5.6.2 — expone las líneas reales que consume JournalFactory (rule.Lines), a diferencia de los campos planos legacy DebitAccountId/CreditAccountId.</summary>
public sealed record PostingRuleLineDto(
    Guid Id, Guid AccountId, string Nature, string AmountKind, short SortOrder);

/// <summary>Fase 5.4 — expone el resultado de ReverseJournalEntryCommand.</summary>
public sealed record JournalEntryDto(
    Guid Id,
    DateOnly EntryDate,
    Guid AccountingPeriodId,
    int FiscalYear,
    string SourceModule,
    string SourceEventType,
    Guid SourceEventId,
    string Description,
    string Status,
    int? EntryNumber,
    DateTime? PostedAtUtc,
    Guid? OriginalJournalEntryId,
    Guid? ReverseJournalEntryId,
    DateTime? ReversedAtUtc,
    string? ReverseReason);
