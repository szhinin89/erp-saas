using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Domain.Modules.Purchasing.Interfaces;

public interface IPurchaseOrderRepository
{
    Task AddAsync(PurchaseOrder order, CancellationToken ct = default);
    Task<PurchaseOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<PurchaseOrder?> GetByIdWithLinesAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     supplierId,
        string?   status,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrder>> GetPendingToInvoiceAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> BillAlreadyLinkedAsync(Guid tenantId, Guid orderId, Guid billId, CancellationToken ct = default);
    Task<IReadOnlyList<(Guid PurchBillId, string InvoiceNumber, DateTime LinkedAt)>>
        GetBillLinksAsync(Guid tenantId, Guid orderId, CancellationToken ct = default);
    Task AddOrderBillLinkAsync(PurchaseOrderBill link, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
