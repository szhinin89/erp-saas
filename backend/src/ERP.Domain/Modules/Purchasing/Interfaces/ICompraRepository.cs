using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Domain.Modules.Purchasing.Interfaces;

public interface IPurchBillRepository
{
    Task AddAsync(PurchBill bill, CancellationToken ct = default);
    Task<PurchBill?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<PurchBill?> GetByIdWithLinesAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsAccessKeyAsync(Guid tenantId, string accessKey, CancellationToken ct = default);
    Task<IReadOnlyList<PurchBill>> GetAsync(
        Guid           tenantId,
        PurchaseStatus? status,
        Guid?          supplierId,
        DateTime?      from,
        DateTime?      to,
        string?        search,
        CancellationToken ct = default);

    Task<IReadOnlyList<PurchWarehouseAlloc>> GetWarehouseAllocsByBillIdAsync(
        Guid tenantId, Guid purchBillId, CancellationToken ct = default);
    Task AddWarehouseAllocAsync(PurchWarehouseAlloc alloc, CancellationToken ct = default);

    Task AddIssuedRetentionAsync(IssuedRetention retention, CancellationToken ct = default);
    Task<IssuedRetention?> GetIssuedRetentionByIdWithLinesAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IssuedRetention>> GetIssuedRetentionsAsync(Guid tenantId, Guid? supplierId, CancellationToken ct = default);

    Task AddPurchNoteAsync(PurchNote note, CancellationToken ct = default);
    Task<PurchNote?> GetPurchNoteByIdWithLinesAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsPurchNoteAccessKeyAsync(Guid tenantId, string accessKey, CancellationToken ct = default);
    Task<IReadOnlyList<PurchNote>> GetPurchNotesAsync(
        Guid    tenantId,
        Guid?   supplierId,
        Guid?   purchBillId,
        Guid?   expenseInvoiceId,
        string? status,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
