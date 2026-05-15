using Microsoft.EntityFrameworkCore;
using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Infrastructure.Persistence.Repositories;

public class AccountingRepository : IAccountingRepository
{
    private readonly ErpDbContext _context;

    public AccountingRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<Account?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Account?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default)
        => await _context.Accounts
            .FirstOrDefaultAsync(a => a.Code == new AccountCode(code), ct);

    public async Task<IReadOnlyList<Account>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Accounts
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Account> Items, int TotalCount)> GetAccountsPageAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.Accounts.AsQueryable();

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<bool> ExistsAsync(string code, Guid tenantId, CancellationToken ct = default)
        => await _context.Accounts
            .AnyAsync(a => a.Code == new AccountCode(code), ct);

    public async Task AddAsync(Account account, CancellationToken ct = default)
        => await _context.Accounts.AddAsync(account, ct);

    public Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        _context.Accounts.Update(account);
        return Task.CompletedTask;
    }

    public async Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default)
        => await _context.JournalEntries.AddAsync(entry, ct);

    public async Task<JournalEntry?> GetJournalEntryByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<JournalEntry>> GetAllJournalEntriesAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.JournalEntries
            .Include(e => e.Lines)
            .OrderByDescending(e => e.Date)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetJournalEntriesPageAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.JournalEntries
            .Include(e => e.Lines)
            .AsQueryable();

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.Date)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<JournalEntry>> GetPostedJournalEntriesWithAccountAsync(
        Guid tenantId,
        Guid accountId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default)
        => await _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .Where(e =>
                e.TenantId == tenantId
                && e.Status == DocumentStatus.Posted
                && e.Date >= desde
                && e.Date <= hasta
                && e.Lines.Any(l => l.AccountId == accountId))
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<(DateTime EntryDate, Guid AccountId, decimal Debit, decimal Credit)>>
        GetPostedLineAmountsByAccountsAsync(
            Guid tenantId,
            IReadOnlyList<Guid> accountIds,
            DateTime desde,
            DateTime hasta,
            CancellationToken ct = default)
    {
        if (accountIds.Count == 0)
            return Array.Empty<(DateTime, Guid, decimal, decimal)>();

        var ids = accountIds.Distinct().ToList();

        var q =
            from line in _context.JournalEntryLines.AsNoTracking()
            join e in _context.JournalEntries.AsNoTracking() on line.JournalEntryId equals e.Id
            where e.TenantId == tenantId
                  && line.TenantId == tenantId
                  && e.Status == DocumentStatus.Posted
                  && e.Date >= desde
                  && e.Date <= hasta
                  && ids.Contains(line.AccountId)
            select new
            {
                e.Date,
                line.AccountId,
                Debit  = line.Debit.Amount,
                Credit = line.Credit.Amount,
            };

        var rows = await q.ToListAsync(ct);
        return rows.Select(r => (r.Date, r.AccountId, r.Debit, r.Credit)).ToList();
    }
}
