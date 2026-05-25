using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SalesOrderRepository : ISalesOrderRepository
{
    private readonly ErpDbContext _db;

    public SalesOrderRepository(ErpDbContext db) => _db = db;

    public async Task<int> GetNextSequentialAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default)
    {
        var max = await _db.SalesOrders
            .AsNoTracking()
            .Where(o => o.SubscriberId == subscriberId && o.BranchId == branchId)
            .Select(o => o.OrderNumber)
            .ToListAsync(ct);

        var seq = max
            .Select(ParseSequential)
            .DefaultIfEmpty(0)
            .Max();

        return seq + 1;
    }

    public async Task AddAsync(SalesOrder order, CancellationToken ct = default)
        => await _db.SalesOrders.AddAsync(order, ct);

    public Task<SalesOrder?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default)
        => _db.SalesOrders
            .Include(o => o.Lines)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.PublicId == publicId, ct);

    public Task<SalesOrder?> GetByPublicIdReadOnlyAsync(Guid publicId, CancellationToken ct = default)
        => _db.SalesOrders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.PublicId == publicId, ct);

    public Task<SalesOrder?> GetByPublicIdHeaderAsync(Guid publicId, CancellationToken ct = default)
        => _db.SalesOrders.FirstOrDefaultAsync(o => o.PublicId == publicId, ct);

    public Task<SalesOrder?> GetByPublicIdHeaderReadOnlyAsync(Guid publicId, CancellationToken ct = default)
        => _db.SalesOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == publicId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task<(IReadOnlyList<SalesOrder> Items, int TotalCount)> GetPagedAsync(
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
        var query = _db.SalesOrders
            .AsNoTracking()
            .Where(o => o.SubscriberId == subscriberId && o.BranchId == branchId);

        if (businessPartnerId.HasValue)
            query = query.Where(o => o.BusinessPartnerId == businessPartnerId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);
        if (dateFrom.HasValue)
            query = query.Where(o => o.IssueDate >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(o => o.IssueDate <= dateTo.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.IssueDate)
            .ThenByDescending(o => o.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public void AssignPendingStatusHistoryIds(ISnowflakeIdGenerator idGenerator)
    {
        foreach (var entry in _db.ChangeTracker.Entries<SalesOrderStatusHistory>())
        {
            if (entry.State == EntityState.Added && entry.Entity.Id == 0)
                entry.Entity.AssignSnowflakeId(idGenerator.NextId());
        }
    }

    public async Task MarkInvoicedAsync(
        long salesOrderId,
        Guid subscriberId,
        Guid userId,
        ISnowflakeIdGenerator idGenerator,
        CancellationToken ct = default)
    {
        var order = await _db.SalesOrders
            .Include(o => o.Lines)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId && o.SubscriberId == subscriberId, ct);

        if (order is null)
            throw new InvalidOperationException("Sales order not found.");

        order.MarkInvoiced(userId);
        AssignPendingStatusHistoryIds(idGenerator);
    }

    public Task<Guid?> GetPublicIdByInternalIdAsync(long id, Guid subscriberId, CancellationToken ct = default)
        => _db.SalesOrders
            .AsNoTracking()
            .Where(o => o.Id == id && o.SubscriberId == subscriberId)
            .Select(o => (Guid?)o.PublicId)
            .FirstOrDefaultAsync(ct);

    private static int ParseSequential(string orderNumber)
    {
        var dash = orderNumber.LastIndexOf('-');
        if (dash < 0 || dash >= orderNumber.Length - 1)
            return 0;
        return int.TryParse(orderNumber[(dash + 1)..], out var n) ? n : 0;
    }
}
