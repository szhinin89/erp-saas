using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.Continuity;

public sealed record AccountingPostingDto(
    Guid Id,
    string SourceModule,
    string FactType,
    Guid SourceEventId,
    string? SourceDocumentNumber,
    DateOnly BusinessDate,
    DateOnly EntryDate,
    AccountingPostingStatus Status,
    string? ErrorCode,
    string? ErrorMessage,
    int Attempts,
    DateTime? LastAttemptAtUtc,
    Guid? JournalEntryId,
    DateTime CreatedAt,
    Guid CreatedBy
)
{
    public static AccountingPostingDto From(AccountingPosting p) =>
        new(
            p.Id,
            p.SourceModule,
            p.FactType,
            p.SourceEventId,
            p.SourceDocumentNumber,
            p.BusinessDate,
            p.EntryDate,
            p.Status,
            p.ErrorCode,
            p.ErrorMessage,
            p.Attempts,
            p.LastAttemptAtUtc,
            p.JournalEntryId,
            p.CreatedAt,
            p.CreatedBy
        );
}

public sealed record AccountingPostingAttemptDto(
    Guid Id,
    int AttemptNumber,
    AccountingPostingAttemptTrigger Trigger,
    Guid TriggeredBy,
    string? CorrelationId,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    AccountingPostingStatus Outcome,
    string? ErrorCode,
    string? ErrorMessage,
    Guid? JournalEntryId
)
{
    public static AccountingPostingAttemptDto From(AccountingPostingAttempt a) =>
        new(
            a.Id,
            a.AttemptNumber,
            a.Trigger,
            a.TriggeredBy,
            a.CorrelationId,
            a.StartedAtUtc,
            a.FinishedAtUtc,
            a.Outcome,
            a.ErrorCode,
            a.ErrorMessage,
            a.JournalEntryId
        );
}

public sealed record AccountingPostingDetailDto(
    AccountingPostingDto Posting,
    IReadOnlyList<AccountingPostingAttemptDto> AttemptHistory
);

public sealed record AccountingOperationalPolicyVersionDto(
    Guid Id,
    AccountingOperationalMode Mode,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Reason,
    DateTime CreatedAt,
    Guid CreatedBy
)
{
    public static AccountingOperationalPolicyVersionDto From(AccountingOperationalPolicy p) =>
        new(p.Id, p.Mode, p.EffectiveFrom, p.EffectiveTo, p.Reason, p.CreatedAt, p.CreatedBy);
}

/// <param name="IsConfigured">La Company ya eligió explícitamente su modo operativo.</param>
/// <param name="CurrentMode">Modo vigente hoy (fecha de la empresa); null si ninguna versión cubre hoy.</param>
/// <param name="DisabledAvailable">
/// Disabled aún no puede seleccionarse: hay integraciones automáticas fuera de
/// ACCOUNTING-CONTINUITY-01 que no convergieron a la obligación durable.
/// </param>
public sealed record AccountingOperationalPolicyDto(
    bool IsAccountingRequired,
    bool IsConfigured,
    AccountingOperationalMode? CurrentMode,
    DateOnly Today,
    bool DisabledAvailable,
    IReadOnlyList<AccountingOperationalPolicyVersionDto> Versions
);
