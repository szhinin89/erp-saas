using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Domain.Modules.Purchasing.Interfaces;

public interface IPurchBillRepository
{
    Task AddAsync(PurchBill bill, CancellationToken ct = default);
    Task<PurchBill?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<PurchBill?> GetByIdWithLinesAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsAccessKeyAsync(Guid subscriberId, string accessKey, CancellationToken ct = default);
    Task<IReadOnlyList<PurchBill>> GetAsync(
        Guid           subscriberId,
        PurchaseStatus? status,
        Guid?          supplierId,
        DateTime?      from,
        DateTime?      to,
        string?        search,
        CancellationToken ct = default);

    Task<IReadOnlyList<PurchWarehouseAlloc>> GetWarehouseAllocsByBillIdAsync(
        Guid subscriberId, Guid purchBillId, CancellationToken ct = default);
    Task AddWarehouseAllocAsync(PurchWarehouseAlloc alloc, CancellationToken ct = default);

    Task AddIssuedRetentionAsync(IssuedRetention retention, CancellationToken ct = default);
    Task<IssuedRetention?> GetIssuedRetentionByIdWithLinesAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IssuedRetention>> GetIssuedRetentionsAsync(Guid subscriberId, Guid? supplierId, CancellationToken ct = default);

    Task AddPurchNoteAsync(PurchNote note, CancellationToken ct = default);
    Task<PurchNote?> GetPurchNoteByIdWithLinesAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsPurchNoteAccessKeyAsync(Guid subscriberId, string accessKey, CancellationToken ct = default);
    Task<IReadOnlyList<PurchNote>> GetPurchNotesAsync(
        Guid    subscriberId,
        Guid?   supplierId,
        Guid?   purchBillId,
        Guid?   expenseInvoiceId,
        string? status,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
