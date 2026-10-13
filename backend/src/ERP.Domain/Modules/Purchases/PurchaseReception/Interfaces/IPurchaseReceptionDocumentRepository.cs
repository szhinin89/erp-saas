using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;

public interface IPurchaseReceptionDocumentRepository
{
    Task AddAsync(PurchaseReceptionDocument document, CancellationToken ct = default);
    Task<PurchaseReceptionDocument?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    /// <summary>Item Matching: resuelve el documento padre de una línea de recepción por su Id.</summary>
    Task<PurchaseReceptionDocument?> GetByLineIdAsync(
        Guid tenantId,
        Guid lineId,
        CancellationToken ct = default
    );

    /// <summary>
    /// PURCHASE-DRAFT-RECOVERY F1 — igual que <see cref="GetByLineIdAsync"/> pero con la evidencia
    /// completa de cada línea (impuestos y datos adicionales del XML), sin tracking: solo lectura
    /// para reabrir un borrador de compra.
    /// </summary>
    Task<PurchaseReceptionDocument?> GetByLineIdWithEvidenceAsync(
        Guid tenantId,
        Guid lineId,
        CancellationToken ct = default
    );
    Task<(IReadOnlyList<PurchaseReceptionDocument> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<bool> ExistsByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    );

    /// <summary>Necesario para el flujo de deduplicación de importación: si ya existe, se reutiliza en vez de crear otro.</summary>
    Task<PurchaseReceptionDocument?> GetByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    );

    /// <summary>
    /// FASE 5 PURCHASE-UX — recepciones por id, mismo alcance tenant + empresa que el resto del
    /// repositorio (los ids ajenos simplemente no vuelven). Solo lectura: sin tracking, sin líneas ni
    /// XML — para refrescar los estados de la bandeja de Recepción.
    /// </summary>
    Task<IReadOnlyList<PurchaseReceptionDocument>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );
    Task SaveChangesAsync(CancellationToken ct = default);
}
