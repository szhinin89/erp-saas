using ERP.Domain.Modules.Purchases.Entities;

namespace ERP.Domain.Modules.Purchases.Interfaces;

public interface IPurchaseInvoiceRepository
{
    Task<PurchaseInvoice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<PurchaseInvoice?> GetByAccessKeyAsync(Guid tenantId, string accessKey, CancellationToken ct = default);
    /// <summary>
    /// <c>LineCounts</c> se resuelve con un COUNT agrupado, no con <c>Include(Lines)</c> —
    /// el listado no necesita las líneas completas de cada compra, solo su cantidad.
    /// </summary>
    Task<(IReadOnlyList<PurchaseInvoice> Items, IReadOnlyDictionary<Guid, int> LineCounts, int Total)> GetPagedAsync(Guid tenantId, string? search, string? status, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(PurchaseInvoice invoice, CancellationToken ct = default);
    Task RemoveLinesByInvoiceAsync(Guid invoiceId, IEnumerable<PurchaseInvoiceDetail> newLines, CancellationToken ct = default);
    Task ClearScheduleTrackingAsync(Guid invoiceId, CancellationToken ct = default);
    void ReattachSchedulesAsAdded(PurchaseInvoice invoice);
    Task<PurchasePayable?> GetPayableByPurchaseIdAsync(Guid tenantId, Guid purchaseId, CancellationToken ct = default);
    Task<IssuedWithholding?> GetWithholdingByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IssuedWithholding?> GetWithholdingByPurchaseIdAsync(Guid tenantId, Guid purchaseId, CancellationToken ct = default);
    void TrackPayable(PurchasePayable payable);
    void TrackCommunication(PurchaseCommunication communication);
    void TrackWithholding(IssuedWithholding withholding);
    Task SaveChangesAsync(CancellationToken ct = default);
}
