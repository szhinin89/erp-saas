using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;

namespace ERP.Domain.Modules.Commercial.Interfaces;

public interface ISalesOrderRepository
{
    Task<int> GetNextSequentialAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default);
    Task AddAsync(SalesOrder order, CancellationToken ct = default);
    Task<SalesOrder?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);
    Task<SalesOrder?> GetByPublicIdReadOnlyAsync(Guid publicId, CancellationToken ct = default);
    Task<SalesOrder?> GetByPublicIdHeaderAsync(Guid publicId, CancellationToken ct = default);
    Task<SalesOrder?> GetByPublicIdHeaderReadOnlyAsync(Guid publicId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    void AssignPendingStatusHistoryIds(ISnowflakeIdGenerator idGenerator);
    Task<(IReadOnlyList<SalesOrder> Items, int TotalCount)> GetPagedAsync(
        Guid subscriberId,
        Guid branchId,
        int pageNumber,
        int pageSize,
        Guid? businessPartnerId,
        string? status,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken ct = default);
    Task MarkInvoicedAsync(
        long salesOrderId,
        Guid subscriberId,
        Guid userId,
        ISnowflakeIdGenerator idGenerator,
        CancellationToken ct = default);
    Task<Guid?> GetPublicIdByInternalIdAsync(long id, Guid subscriberId, CancellationToken ct = default);
}
