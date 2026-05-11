using ERP.Domain.Modules.Contabilidad.Entities;

namespace ERP.Domain.Modules.Contabilidad.Interfaces;

public interface IAccountingRepository
{
    Task<Account?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<Account?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Account>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Account> Items, int TotalCount)> GetAccountsPageAsync(Guid tenantId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<bool> ExistsAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task UpdateAsync(Account account, CancellationToken ct = default);
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default);
    Task<JournalEntry?> GetJournalEntryByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> GetAllJournalEntriesAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetJournalEntriesPageAsync(Guid tenantId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
