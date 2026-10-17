using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class ManualJournalRepository(ErpDbContext db) : IManualJournalRepository
{
    public Task<ManualJournalAttempt?> FindAttemptAsync(Guid tenantId, Guid companyId, Guid entryId, Guid key, CancellationToken ct) =>
        db.Set<ManualJournalAttempt>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId
            && x.CompanyId == companyId && x.JournalEntryId == entryId && x.IdempotencyKey == key, ct);
    public async Task<IReadOnlyList<ManualJournalAttempt>> ListAttemptsAsync(Guid tenantId, Guid companyId, Guid entryId, CancellationToken ct) =>
        await db.Set<ManualJournalAttempt>().AsNoTracking().Where(x => x.TenantId == tenantId
            && x.CompanyId == companyId && x.JournalEntryId == entryId).OrderByDescending(x => x.AttemptedAtUtc).ToListAsync(ct);
    public Task AddAttemptAsync(ManualJournalAttempt attempt, CancellationToken ct) =>
        db.Set<ManualJournalAttempt>().AddAsync(attempt, ct).AsTask();
}
