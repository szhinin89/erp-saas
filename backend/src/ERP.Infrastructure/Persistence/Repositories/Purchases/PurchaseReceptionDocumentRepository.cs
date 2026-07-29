using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Purchases;

public sealed class PurchaseReceptionDocumentRepository : IPurchaseReceptionDocumentRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public PurchaseReceptionDocumentRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<PurchaseReceptionDocument> Scoped(Guid tenantId) =>
        _db.PurchaseReceptionDocuments.ForOperationalScope(tenantId, _company);

    public Task AddAsync(PurchaseReceptionDocument document, CancellationToken ct = default) =>
        _db.PurchaseReceptionDocuments.AddAsync(document, ct).AsTask();

    public Task<PurchaseReceptionDocument?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) => Scoped(tenantId).Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<PurchaseReceptionDocument?> GetByLineIdAsync(
        Guid tenantId,
        Guid lineId,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Lines.Any(l => l.Id == lineId), ct);

    public async Task<(IReadOnlyList<PurchaseReceptionDocument> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> ExistsByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    ) => Scoped(tenantId).AnyAsync(x => x.AccessKey == accessKey, ct);

    public Task<PurchaseReceptionDocument?> GetByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    ) => Scoped(tenantId).FirstOrDefaultAsync(x => x.AccessKey == accessKey, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
