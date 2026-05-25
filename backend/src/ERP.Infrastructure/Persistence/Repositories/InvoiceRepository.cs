using ERP.Application.Common;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Fiscal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly IPlatformQueryAccessor _platform;

    public InvoiceRepository(
        ErpDbContext db,
        ICurrentCompany company,
        ICurrentSubscriber currentSubscriber,
        IPlatformQueryAccessor platform)
    {
        _db = db;
        _company = company;
        _currentSubscriber = currentSubscriber;
        _platform = platform;
    }

    private IQueryable<Invoice> Scoped(Guid subscriberId) =>
        _db.FiscalInvoices.ForOperationalScope(subscriberId, _company);

    public Task AddAsync(Invoice invoice, CancellationToken ct = default)
        => _db.FiscalInvoices.AddAsync(invoice, ct).AsTask();

    public Task<Invoice?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default)
        => Scoped(_currentSubscriber.SubscriberId)
            .Include(i => i.Lines)
            .Include(i => i.Electronic)
            .Include(i => i.StatusHistory)
            .FirstOrDefaultAsync(i => i.PublicId == publicId, ct);

    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> GetPagedAsync(
        Guid subscriberId,
        int pageNumber,
        int pageSize,
        Guid? businessPartnerId,
        DateTime? from,
        DateTime? to,
        string? status,
        string? search,
        CancellationToken ct = default)
    {
        var query = Scoped(subscriberId).AsNoTracking();

        if (businessPartnerId.HasValue)
            query = query.Where(i => i.BusinessPartnerId == businessPartnerId.Value);
        if (from.HasValue)
            query = query.Where(i => i.IssueDate >= from.Value);
        if (to.HasValue)
            query = query.Where(i => i.IssueDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i =>
                i.AccessKey.Contains(term) ||
                i.Sequential.Contains(term) ||
                i.BuyerNameSnapshot.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<InvoiceRetryCandidate>> ListPendingElectronicRetryAsync(CancellationToken ct = default)
    {
        var query = _platform.Unfiltered(_db.FiscalInvoices, PlatformQueryReason.BackgroundJob)
            .Where(i => i.Status == Invoice.Statuses.SendError || i.Status == Invoice.Statuses.Rejected);

        return await query
            .Select(i => new InvoiceRetryCandidate(i.PublicId, i.SubscriberId))
            .ToListAsync(ct);
    }

    public Task<Guid?> GetPublicIdByInternalIdAsync(long id, Guid subscriberId, CancellationToken ct = default)
        => Scoped(subscriberId)
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (Guid?)i.PublicId)
            .FirstOrDefaultAsync(ct);
}
