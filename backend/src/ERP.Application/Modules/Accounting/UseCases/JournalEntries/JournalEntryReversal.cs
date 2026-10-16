using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.JournalEntries;

/// <summary>Núcleo único del reverso. El llamador mantiene la transacción hasta persistir.</summary>
public sealed class JournalEntryReversal(
    IJournalEntryRepository journal,
    IAccountingPeriodRepository periods,
    IJournalEntrySequenceRepository sequences,
    IAccountingOperationalPolicyRepository policies)
{
    public async Task<Result<AccountingPeriod>> RequirePeriodAsync(
        Guid tenantId, Guid companyId, DateOnly effectiveDate, CancellationToken ct)
    {
        await policies.AcquireSharedLockAsync(companyId, ct);
        var period = await periods.FindContainingDateAsync(tenantId, companyId, effectiveDate, ct);
        return period is null
            ? Result<AccountingPeriod>.ValidationFailure("No existe un período Open para la fecha efectiva del reverso.", "PERIOD_NOT_OPEN")
            : new PostingPeriodGuard().Ensure(period);
    }

    public async Task<Result<JournalEntry>> ReverseAsync(JournalEntry original, Guid reversedBy,
        string reason, DateOnly effectiveDate, CancellationToken ct)
    {
        await policies.AcquireSharedLockAsync(original.CompanyId, ct);
        await journal.AcquireIdempotencyLockAsync(original.CompanyId, "Accounting", original.Id, "Reversal", ct);
        var existing = await journal.FindByKeyAsync(original.TenantId, original.CompanyId,
            "Accounting", "Reversal", original.Id, ct);
        if (existing is not null)
            return Result<JournalEntry>.Success(existing);
        var period = await RequirePeriodAsync(original.TenantId, original.CompanyId, effectiveDate, ct);
        if (!period.IsSuccess)
            return Result<JournalEntry>.ValidationFailure(period.Error!, period.Code);
        var number = await sequences.ReserveNextNumberAsync(original.TenantId, original.CompanyId,
            period.Value!.FiscalYear, ct);
        JournalEntry reversal;
        try
        {
            reversal = original.Reverse(reversedBy, number, reason, effectiveDate,
                period.Value.Id, period.Value.FiscalYear);
        }
        catch (ArgumentException ex)
        {
            return Result<JournalEntry>.ValidationFailure(ex.Message);
        }
        await journal.AddAsync(reversal, ct);
        return Result<JournalEntry>.Success(reversal);
    }
}
