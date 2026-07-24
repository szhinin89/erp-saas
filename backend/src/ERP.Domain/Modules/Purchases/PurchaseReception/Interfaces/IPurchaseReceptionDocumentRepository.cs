using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;

public interface IPurchaseReceptionDocumentRepository
{
    Task AddAsync(PurchaseReceptionDocument document, CancellationToken ct = default);
    Task<PurchaseReceptionDocument?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<PurchaseReceptionDocument> Items, int Total)> GetPagedAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<bool> ExistsByAccessKeyAsync(Guid tenantId, string accessKey, CancellationToken ct = default);
    /// <summary>Necesario para el flujo de deduplicación de importación: si ya existe, se reutiliza en vez de crear otro.</summary>
    Task<PurchaseReceptionDocument?> GetByAccessKeyAsync(Guid tenantId, string accessKey, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
