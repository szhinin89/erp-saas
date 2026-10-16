using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>Registra el reverso obligatorio en la transacción operativa; solo el gate de período bloquea.</summary>
public interface IAccountingReversalRecorder
{
    Task RecordSourceAsync(Guid tenantId, Guid companyId, string sourceModule, string factType,
        Guid sourceId, string reason, DateTime effectiveAtUtc, CancellationToken ct);
}

public sealed class AccountingReversalRecorder(
    AccountingPostingCancellation cancellation,
    JournalEntryReversal reversal,
    IJournalEntryRepository journal,
    IAccountingPostingRepository postings,
    AccountingPostingDispatchQueue queue,
    ICompanyClock clock,
    ICurrentUser user,
    IAuditContext audit,
    IAccountingOperationalPolicyRepository policies) : IAccountingReversalRecorder
{
    public async Task RecordSourceAsync(Guid tenantId, Guid companyId, string sourceModule,
        string factType, Guid sourceId, string reason, DateTime effectiveAtUtc, CancellationToken ct)
    {
        var original = await cancellation.PrepareAsync(tenantId, companyId, sourceModule, factType, sourceId, ct);
        if (original is null) return;
        var date = await clock.LocalDateAsync(companyId, tenantId, effectiveAtUtc, ct);
        await RecordAsync(original, reason, date, ct);
    }

    public async Task RecordAsync(JournalEntry original, string reason, DateOnly effectiveDate, CancellationToken ct)
    {
        // El lock compartido precede a identidad/fila, igual que el cierre y el registro normal.
        await policies.AcquireSharedLockAsync(original.CompanyId, ct);
        await journal.AcquireIdempotencyLockAsync(original.CompanyId, "Accounting", original.Id, "Reversal", ct);
        if (await journal.FindByKeyAsync(original.TenantId, original.CompanyId, "Accounting", "Reversal", original.Id, ct) is not null)
            return;
        if (await postings.FindByKeyAsync(original.TenantId, original.CompanyId, "Accounting", "Reversal", original.Id, ct) is not null)
            return;
        var period = await reversal.RequirePeriodAsync(original.TenantId, original.CompanyId, effectiveDate, ct);
        if (!period.IsSuccess) throw new DomainRuleViolationException(period.Error!);
        var payload = new AccountingPostingPayload(0, 0, 0, 0, 0,
            OriginalJournalEntryId: original.Id, ReversalReason: reason.Trim());
        var posting = AccountingPosting.CreatePending(original.TenantId, original.CompanyId,
            "Accounting", "Reversal", original.Id, original.EntryNumber?.ToString(), effectiveDate,
            effectiveDate, AccountingPostingPayload.CurrentVersion, payload.Serialize(), user.UserId,
            audit.Actor.CorrelationId);
        await postings.AddAsync(posting, ct);
        queue.Enqueue(posting.Id);
    }
}
