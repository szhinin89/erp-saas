using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>Hecho contable de origen a convertir en obligación durable.</summary>
public sealed record AccountingPostingRequest(
    Guid TenantId,
    Guid CompanyId,
    string SourceModule,
    string FactType,
    Guid SourceEventId,
    string? SourceDocumentNumber,
    DateOnly BusinessDate,
    DateOnly EntryDate,
    AccountingPostingPayload Payload
);

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — crea la obligación Pending dentro de la misma transacción que el
/// núcleo operativo (llamado desde los traductores de dominio durante
/// <c>ErpDbContext.SaveChangesAsync</c>). Nunca invoca el Posting Engine: el intento ocurre después
/// del commit (<see cref="AccountingPostingExecutor"/>). Fail-closed si la Company no eligió modo
/// operativo: sin decisión no puede saberse si la obligación es exigible, así que la operación no
/// se confirma (docs/architecture/accounting.md §4).
/// </summary>
public sealed class AccountingPostingRecorder
{
    private readonly IAccountingOperationalPolicyRepository _policies;
    private readonly AccountingOperationalDecision _decision;
    private readonly IAccountingPostingRepository _postings;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly AccountingPostingDispatchQueue _queue;
    private readonly ICurrentUser _user;
    private readonly IAuditContext _audit;

    public AccountingPostingRecorder(
        IAccountingOperationalPolicyRepository policies,
        AccountingOperationalDecision decision,
        IAccountingPostingRepository postings,
        IJournalEntryRepository journalEntries,
        AccountingPostingDispatchQueue queue,
        ICurrentUser user,
        IAuditContext audit
    )
    {
        _policies = policies;
        _decision = decision;
        _postings = postings;
        _journalEntries = journalEntries;
        _queue = queue;
        _user = user;
        _audit = audit;
    }

    public async Task RecordAsync(AccountingPostingRequest request, CancellationToken ct)
    {
        // Serializa con cambios de política y con el cierre de período (lock exclusivo) hasta el
        // commit del núcleo: ninguno puede decidir entre esta lectura y la persistencia.
        await _policies.AcquireSharedLockAsync(request.CompanyId, ct);

        var applicability = await _decision.ResolveAsync(
            request.TenantId,
            request.CompanyId,
            request.BusinessDate,
            ct
        );
        if (applicability == AccountingApplicability.NotConfigured)
            throw new DomainRuleViolationException(AccountingOperationalDecision.NotConfiguredMessage);
        if (applicability == AccountingApplicability.NotApplicable)
            return;

        // Mismo advisory lock de idempotencia que el Posting Engine para esta identidad: dos
        // registros concurrentes del mismo hecho se serializan y el segundo ve la obligación ya
        // comiteada, sin violar el índice único.
        await _journalEntries.AcquireIdempotencyLockAsync(
            request.CompanyId,
            request.SourceModule,
            request.SourceEventId,
            request.FactType,
            ct
        );
        var existing = await _postings.FindByKeyAsync(
            request.TenantId,
            request.CompanyId,
            request.SourceModule,
            request.FactType,
            request.SourceEventId,
            ct
        );
        if (existing is not null)
            return;

        var posting = AccountingPosting.CreatePending(
            request.TenantId,
            request.CompanyId,
            request.SourceModule,
            request.FactType,
            request.SourceEventId,
            request.SourceDocumentNumber,
            request.BusinessDate,
            request.EntryDate,
            AccountingPostingPayload.CurrentVersion,
            request.Payload.Serialize(),
            _user.UserId,
            _audit.Actor.CorrelationId
        );
        await _postings.AddAsync(posting, ct);
        _queue.Enqueue(posting.Id);
    }
}
