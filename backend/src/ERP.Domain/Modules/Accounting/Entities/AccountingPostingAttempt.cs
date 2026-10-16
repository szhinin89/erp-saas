using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — historial append-only de un intento ejecutado sobre una
/// <see cref="AccountingPosting"/>. Solo se crea desde <see cref="AccountingPosting"/>; no tiene
/// métodos de mutación (docs/architecture/accounting.md §14: cada intento queda durable, no solo
/// en logs).
/// </summary>
public sealed class AccountingPostingAttempt
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int ErrorCodeMaxLength = 100;
    public const int ErrorMessageMaxLength = 2000;
    public const int CorrelationIdMaxLength = 100;

    public Guid CompanyId { get; private set; }
    public Guid AccountingPostingId { get; private set; }
    public int AttemptNumber { get; private set; }
    public AccountingPostingAttemptTrigger Trigger { get; private set; }
    public Guid TriggeredBy { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime FinishedAtUtc { get; private set; }
    public AccountingPostingStatus Outcome { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? JournalEntryId { get; private set; }

    private AccountingPostingAttempt() { }

    internal static AccountingPostingAttempt Create(
        AccountingPosting posting,
        int attemptNumber,
        AccountingPostingAttemptTrigger trigger,
        Guid triggeredBy,
        string? correlationId,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        AccountingPostingStatus outcome,
        string? errorCode,
        string? errorMessage,
        Guid? journalEntryId
    )
    {
        var attempt = new AccountingPostingAttempt
        {
            Id = Guid.NewGuid(),
            TenantId = posting.TenantId,
            CompanyId = posting.CompanyId,
            AccountingPostingId = posting.Id,
            AttemptNumber = attemptNumber,
            Trigger = trigger,
            TriggeredBy = triggeredBy,
            CorrelationId = AccountingPosting.Truncate(correlationId, CorrelationIdMaxLength),
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = finishedAtUtc,
            Outcome = outcome,
            ErrorCode = AccountingPosting.Truncate(errorCode, ErrorCodeMaxLength),
            ErrorMessage = AccountingPosting.Truncate(errorMessage, ErrorMessageMaxLength),
            JournalEntryId = journalEntryId,
        };
        attempt.SetCreated(triggeredBy);
        return attempt;
    }
}
