using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Sales.Interfaces;

public interface ISalesRepository
{
    Task AddBillAsync(SalesBill bill, CancellationToken ct = default);
    Task<SalesBill?> GetBillByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SalesBill>> GetBillsAsync(
        Guid      subscriberId,
        DateTime? from,
        DateTime? to,
        string?   status,
        CancellationToken ct = default);
    Task<(IReadOnlyList<SalesBill> Items, int TotalCount)> GetBillsPagedAsync(
        Guid      subscriberId,
        int       pageNumber,
        int       pageSize,
        Guid?     customerId,
        DateTime? from,
        DateTime? to,
        string?   status,
        string?   search,
        CancellationToken ct = default);

    Task AddNoteAsync(SalesNote note, CancellationToken ct = default);
    Task<SalesNote?> GetNoteByIdWithLinesAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SalesNote>> GetNotesAsync(
        Guid    subscriberId,
        Guid?   originalBillId,
        string? status,
        CancellationToken ct = default);

    Task AddRetentionAsync(SalesRetention retention, CancellationToken ct = default);
    Task<IReadOnlyList<SalesRetention>> GetRetentionsAsync(Guid subscriberId, CancellationToken ct = default);
    Task<bool> ExistsRetentionAccessKeyAsync(Guid subscriberId, string accessKey, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
