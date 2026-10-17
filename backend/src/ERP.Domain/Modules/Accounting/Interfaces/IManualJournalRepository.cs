using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IManualJournalRepository
{
    Task<ManualJournalAttempt?> FindAttemptAsync(Guid tenantId, Guid companyId, Guid entryId, Guid key, CancellationToken ct);
    Task<IReadOnlyList<ManualJournalAttempt>> ListAttemptsAsync(Guid tenantId, Guid companyId, Guid entryId, CancellationToken ct);
    Task AddAttemptAsync(ManualJournalAttempt attempt, CancellationToken ct);
}
