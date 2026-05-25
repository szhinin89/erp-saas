using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;

namespace ERP.Domain.Modules.Commercial.Interfaces;

public interface IQuoteRepository
{
    Task<int> GetNextSequentialAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default);
    Task AddAsync(Quote quote, CancellationToken ct = default);
    Task<Quote?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);
    Task<Quote?> GetByPublicIdReadOnlyAsync(Guid publicId, CancellationToken ct = default);
    Task<(IReadOnlyList<Quote> Items, int TotalCount)> GetPagedAsync(
        Guid subscriberId,
        Guid branchId,
        int pageNumber,
        int pageSize,
        Guid? businessPartnerId,
        string? status,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken ct = default);
    void AssignPendingStatusHistoryIds(ISnowflakeIdGenerator idGenerator);
    Task SaveChangesAsync(CancellationToken ct = default);
}
