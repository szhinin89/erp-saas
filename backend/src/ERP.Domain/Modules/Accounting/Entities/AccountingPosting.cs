using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — obligación contable durable común (docs/architecture/accounting.md
/// §4–§6). Nace en la misma transacción que el núcleo operativo cuando Accounting aplica al hecho y
/// se resuelve solo mediante intentos explícitos (inicial tras el commit o reproceso manual), nunca
/// por reintento automático. Su identidad
/// <c>(CompanyId, SourceModule, SourceEventId, FactType)</c> es la misma que usa el Posting Engine
/// para la idempotencia del asiento, por lo que un reproceso nunca genera otra identidad.
/// <see cref="Payload"/> conserva los importes del origen (versionados por
/// <see cref="PayloadVersion"/>); las cuentas se resuelven con la configuración vigente en cada
/// intento.
/// </summary>
public sealed class AccountingPosting : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int SourceModuleMaxLength = 60;
    public const int FactTypeMaxLength = 60;
    public const int SourceDocumentNumberMaxLength = 100;
    public const int ErrorCodeMaxLength = AccountingPostingAttempt.ErrorCodeMaxLength;
    public const int ErrorMessageMaxLength = AccountingPostingAttempt.ErrorMessageMaxLength;
    public const int CorrelationIdMaxLength = AccountingPostingAttempt.CorrelationIdMaxLength;
    public const int ReasonMaxLength = AccountingPostingAttempt.ReasonMaxLength;

    public Guid CompanyId { get; private set; }
    public string SourceModule { get; private set; } = null!;
    public string FactType { get; private set; } = null!;
    public Guid SourceEventId { get; private set; }
    public string? SourceDocumentNumber { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public int PayloadVersion { get; private set; }
    public string Payload { get; private set; } = null!;
    public AccountingPostingStatus Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public string? CorrelationId { get; private set; }

    private AccountingPosting() { }

    public static AccountingPosting CreatePending(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid sourceEventId,
        string? sourceDocumentNumber,
        DateOnly businessDate,
        DateOnly entryDate,
        int payloadVersion,
        string payload,
        Guid createdBy,
        string? correlationId
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(sourceModule))
            throw new ArgumentException("El módulo de origen es obligatorio.", nameof(sourceModule));
        if (string.IsNullOrWhiteSpace(factType))
            throw new ArgumentException("El tipo de hecho es obligatorio.", nameof(factType));
        if (sourceEventId == Guid.Empty)
            throw new ArgumentException("El hecho de origen es obligatorio.", nameof(sourceEventId));
        if (businessDate == default || entryDate == default)
            throw new ArgumentException("Las fechas del hecho son obligatorias.", nameof(entryDate));
        if (payloadVersion < 1 || string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Los datos del hecho son obligatorios.", nameof(payload));

        var posting = new AccountingPosting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            SourceModule = sourceModule.Trim(),
            FactType = factType.Trim(),
            SourceEventId = sourceEventId,
            SourceDocumentNumber = Truncate(sourceDocumentNumber?.Trim(), SourceDocumentNumberMaxLength),
            BusinessDate = businessDate,
            EntryDate = entryDate,
            PayloadVersion = payloadVersion,
            Payload = payload,
            Status = AccountingPostingStatus.Pending,
            CorrelationId = Truncate(correlationId, CorrelationIdMaxLength),
        };
        posting.SetCreated(createdBy);
        return posting;
    }

    public bool IsResolved =>
        Status is AccountingPostingStatus.Posted or AccountingPostingStatus.Canceled;

    /// <summary>Resultado exitoso de un intento: asiento persistido y enlazado en la misma transacción.</summary>
    public AccountingPostingAttempt RecordPosted(
        Guid journalEntryId,
        AccountingPostingAttemptTrigger trigger,
        Guid triggeredBy,
        string? correlationId,
        string? reason,
        DateTime startedAtUtc,
        DateTime finishedAtUtc
    )
    {
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("El asiento es obligatorio.", nameof(journalEntryId));
        EnsureAttemptable();
        var normalizedReason = NormalizeReason(trigger, reason);

        Status = AccountingPostingStatus.Posted;
        JournalEntryId = journalEntryId;
        ErrorCode = null;
        ErrorMessage = null;
        return RegisterAttempt(trigger, triggeredBy, correlationId, normalizedReason, startedAtUtc, finishedAtUtc, null, null);
    }

    /// <summary>Resultado fallido de un intento: error durable, sin asiento.</summary>
    public AccountingPostingAttempt RecordFailed(
        string? errorCode,
        string? errorMessage,
        AccountingPostingAttemptTrigger trigger,
        Guid triggeredBy,
        string? correlationId,
        string? reason,
        DateTime startedAtUtc,
        DateTime finishedAtUtc
    )
    {
        EnsureAttemptable();
        var normalizedReason = NormalizeReason(trigger, reason);

        Status = AccountingPostingStatus.Failed;
        ErrorCode = Truncate(string.IsNullOrWhiteSpace(errorCode) ? "POSTING_FAILED" : errorCode, ErrorCodeMaxLength);
        ErrorMessage = Truncate(errorMessage, ErrorMessageMaxLength);
        return RegisterAttempt(
            trigger,
            triggeredBy,
            correlationId,
            normalizedReason,
            startedAtUtc,
            finishedAtUtc,
            ErrorCode,
            ErrorMessage
        );
    }

    /// <summary>
    /// Enlaza un asiento ya existente para esta identidad sin ejecutar un intento (p. ej. falló la
    /// persistencia del resultado y una anulación lo descubre). No crea otro asiento.
    /// </summary>
    public void ResolveWithExistingEntry(Guid journalEntryId, Guid updatedBy)
    {
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("El asiento es obligatorio.", nameof(journalEntryId));
        if (Status == AccountingPostingStatus.Posted && JournalEntryId == journalEntryId)
            return;
        EnsureAttemptable();

        Status = AccountingPostingStatus.Posted;
        JournalEntryId = journalEntryId;
        ErrorCode = null;
        ErrorMessage = null;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Extingue una obligación aún no publicada porque su origen se anuló (§5). Una obligación
    /// Posted nunca se cancela: su efecto se corrige con el reverso del asiento.
    /// </summary>
    public void Cancel(Guid canceledBy)
    {
        if (Status == AccountingPostingStatus.Canceled)
            return;
        if (Status == AccountingPostingStatus.Posted)
            throw new DomainRuleViolationException(
                "La contabilización ya fue publicada; corríjala con el reverso del asiento."
            );

        Status = AccountingPostingStatus.Canceled;
        SetUpdated(canceledBy);
    }

    private void EnsureAttemptable()
    {
        if (Status == AccountingPostingStatus.Posted)
            throw new DomainRuleViolationException("La contabilización ya está publicada.");
        if (Status == AccountingPostingStatus.Canceled)
            throw new DomainRuleViolationException(
                "La contabilización fue cancelada porque su documento de origen se anuló."
            );
    }

    /// <summary>
    /// Un reproceso manual exige motivo (docs/architecture/accounting.md §14); el intento inicial es
    /// del sistema y no lo lleva.
    /// </summary>
    public static string? NormalizeReason(AccountingPostingAttemptTrigger trigger, string? reason)
    {
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trigger == AccountingPostingAttemptTrigger.Manual && trimmed is null)
            throw new ArgumentException("El motivo del reproceso manual es obligatorio.", nameof(reason));
        if (trimmed is { Length: > ReasonMaxLength })
            throw new ArgumentException(
                $"El motivo no puede superar {ReasonMaxLength} caracteres.",
                nameof(reason)
            );
        return trimmed;
    }

    private AccountingPostingAttempt RegisterAttempt(
        AccountingPostingAttemptTrigger trigger,
        Guid triggeredBy,
        string? correlationId,
        string? reason,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string? errorCode,
        string? errorMessage
    )
    {
        Attempts++;
        LastAttemptAtUtc = finishedAtUtc;
        SetUpdated(triggeredBy);
        return AccountingPostingAttempt.Create(
            this,
            Attempts,
            trigger,
            triggeredBy,
            correlationId,
            reason,
            startedAtUtc,
            finishedAtUtc,
            Status,
            errorCode,
            errorMessage,
            JournalEntryId
        );
    }

    internal static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}
