using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class QuoteRepository : IQuoteRepository
{
    private readonly ErpDbContext _db;

    public QuoteRepository(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetNextSequentialAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default)
    {
        var max = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.SubscriberId == subscriberId && q.BranchId == branchId)
            .Select(q => q.QuoteNumber)
            .ToListAsync(ct);

        var seq = max
            .Select(ParseSequential)
            .DefaultIfEmpty(0)
            .Max();

        return seq + 1;
    }

    public async Task AddAsync(Quote quote, CancellationToken ct = default)
    {
        await _db.Quotes.AddAsync(quote, ct);
    }

    public Task<Quote?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default)
        => _db.Quotes
            .Include(q => q.Lines)
            .Include(q => q.StatusHistory)
            .FirstOrDefaultAsync(q => q.PublicId == publicId, ct);

    public Task<Quote?> GetByPublicIdReadOnlyAsync(Guid publicId, CancellationToken ct = default)
        => _db.Quotes
            .AsNoTracking()
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.PublicId == publicId, ct);

    public async Task<(IReadOnlyList<Quote> Items, int TotalCount)> GetPagedAsync(
        Guid subscriberId,
        Guid branchId,
        int pageNumber,
        int pageSize,
        Guid? businessPartnerId,
        string? status,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken ct = default)
    {
        var query = _db.Quotes
            .AsNoTracking()
            .Where(q => q.SubscriberId == subscriberId && q.BranchId == branchId);

        if (businessPartnerId.HasValue)
            query = query.Where(q => q.BusinessPartnerId == businessPartnerId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(q => q.Status == status);
        if (dateFrom.HasValue)
            query = query.Where(q => q.IssueDate >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(q => q.IssueDate <= dateTo.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(q => q.IssueDate)
            .ThenByDescending(q => q.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public void AssignPendingStatusHistoryIds(ISnowflakeIdGenerator idGenerator)
    {
        foreach (var entry in _db.ChangeTracker.Entries<QuoteStatusHistory>())
        {
            if (entry.State == EntityState.Added && entry.Entity.Id == 0)
                entry.Entity.AssignSnowflakeId(idGenerator.NextId());
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    private static int ParseSequential(string quoteNumber)
    {
        var dash = quoteNumber.LastIndexOf('-');
        if (dash < 0 || dash >= quoteNumber.Length - 1)
            return 0;
        return int.TryParse(quoteNumber[(dash + 1)..], out var n) ? n : 0;
    }
}
