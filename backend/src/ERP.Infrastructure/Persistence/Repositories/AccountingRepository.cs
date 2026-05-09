using Microsoft.EntityFrameworkCore;
using ERP.Domain.Accounting.Entities;
using ERP.Domain.Accounting.Interfaces;
using ERP.Domain.Accounting.ValueObjects;

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
            .OrderBy(a => a.Code.Value)
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
            .OrderBy(a => a.Code.Value)
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
}
