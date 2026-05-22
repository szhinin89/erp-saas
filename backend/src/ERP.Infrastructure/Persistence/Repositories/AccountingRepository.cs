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

    public async Task<Account?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Account?> GetByCodeAsync(string code, Guid subscriberId, CancellationToken ct = default)
        => await _context.Accounts
            .FirstOrDefaultAsync(a => a.Code == new AccountCode(code), ct);

    public async Task<IReadOnlyList<Account>> GetAllByTenantAsync(Guid subscriberId, CancellationToken ct = default)
        => await _context.Accounts
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Account> Items, int TotalCount)> GetAccountsPageAsync(
        Guid subscriberId,
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

    public async Task<bool> ExistsAsync(string code, Guid subscriberId, CancellationToken ct = default)
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

    public async Task<JournalEntry?> GetJournalEntryByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await _context.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<JournalEntry>> GetAllJournalEntriesAsync(Guid subscriberId, CancellationToken ct = default)
        => await _context.JournalEntries
            .Include(e => e.Lines)
            .OrderByDescending(e => e.Date)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetJournalEntriesPageAsync(
        Guid subscriberId,
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
        Guid subscriberId,
        Guid accountId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default)
        => await _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .Where(e =>
                e.SubscriberId == subscriberId
                && e.Status == DocumentStatus.Posted
                && e.Date >= desde
                && e.Date <= hasta
                && e.Lines.Any(l => l.AccountId == accountId))
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<(DateTime EntryDate, Guid AccountId, decimal Debit, decimal Credit)>>
        GetPostedLineAmountsByAccountsAsync(
            Guid subscriberId,
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
            where e.SubscriberId == subscriberId
                  && line.SubscriberId == subscriberId
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

    public async Task<IReadOnlyList<(DateTime Date, string Reference, string Description, decimal Debit, decimal Credit)>>
        GetMayorGeneralLinesAsync(
            Guid subscriberId,
            Guid accountId,
            DateTime desde,
            DateTime hasta,
            CancellationToken ct = default)
    {
        var q =
            from line in _context.JournalEntryLines.AsNoTracking()
            join e in _context.JournalEntries.AsNoTracking() on line.JournalEntryId equals e.Id
            where e.SubscriberId == subscriberId
                  && line.SubscriberId == subscriberId
                  && e.Status == DocumentStatus.Posted
                  && e.Date >= desde
                  && e.Date <= hasta
                  && line.AccountId == accountId
            orderby e.Date, e.Id
            select new
            {
                e.Date,
                e.Reference,
                e.Description,
                Debit  = line.Debit.Amount,
                Credit = line.Credit.Amount,
            };

        var rows = await q.ToListAsync(ct);
        return rows.Select(r => (r.Date, r.Reference, r.Description, r.Debit, r.Credit)).ToList();
    }

    public async Task<IReadOnlyList<(Guid AccountId, decimal TotalDebit, decimal TotalCredit)>>
        GetBalanceComprobacionAsync(
            Guid subscriberId,
            DateTime desde,
            DateTime hasta,
            CancellationToken ct = default)
    {
        var q =
            from line in _context.JournalEntryLines.AsNoTracking()
            join e in _context.JournalEntries.AsNoTracking() on line.JournalEntryId equals e.Id
            where e.SubscriberId == subscriberId
                  && line.SubscriberId == subscriberId
                  && e.Status == DocumentStatus.Posted
                  && e.Date >= desde
                  && e.Date <= hasta
            select new
            {
                line.AccountId,
                Debit  = line.Debit.Amount,
                Credit = line.Credit.Amount,
            };

        var rows = await q.ToListAsync(ct);
        return rows
            .GroupBy(r => r.AccountId)
            .Select(g => (g.Key, g.Sum(x => x.Debit), g.Sum(x => x.Credit)))
            .ToList();
    }
}
