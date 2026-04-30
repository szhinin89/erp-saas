using ERP.Domain.Accounting.Entities;

namespace ERP.Domain.Accounting.Interfaces;

public interface IAccountingRepository
{
    Task<Account?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<Account?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Account>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default);
    Task<JournalEntry?> GetJournalEntryByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> GetAllJournalEntriesAsync(Guid tenantId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
