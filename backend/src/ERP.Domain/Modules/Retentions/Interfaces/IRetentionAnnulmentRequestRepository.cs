using ERP.Domain.Modules.Retentions.Entities;

namespace ERP.Domain.Modules.Retentions.Interfaces;

/// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — persistencia de <see cref="RetentionAnnulmentRequest"/>.</summary>
public interface IRetentionAnnulmentRequestRepository
{
    Task AddAsync(RetentionAnnulmentRequest request, CancellationToken ct = default);

    /// <summary>Solicitud por Id, filtrada por tenant y empresa (fail-closed). Siempre relee de la BD si ya estaba trackeada.</summary>
    Task<RetentionAnnulmentRequest?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default);

    /// <summary>Solicitud abierta (pendiente de presentación o de resolución) de la retención, si existe.</summary>
    Task<RetentionAnnulmentRequest?> GetOpenByRetentionAsync(Guid tenantId, Guid companyId, Guid retentionDocumentId, CancellationToken ct = default);

    /// <summary>Solicitud más reciente de la retención (cualquier estado), para mostrar su situación.</summary>
    Task<RetentionAnnulmentRequest?> GetLatestByRetentionAsync(Guid tenantId, Guid companyId, Guid retentionDocumentId, CancellationToken ct = default);

    /// <summary>
    /// Recuperación (job, cross-tenant): solicitudes ANULADO confirmadas cuyo origen todavía no terminó
    /// de anularse. Solo identificadores; cada una se procesa con el contexto de su tenant/empresa.
    /// </summary>
    Task<IReadOnlyList<(Guid TenantId, Guid CompanyId, Guid RequestId)>> GetPendingFinalizationAsync(
        int take,
        CancellationToken ct = default
    );

    /// <summary>
    /// ZH-RETENTION-SRI-ANNULMENT-01B — polling (job, cross-tenant): solicitudes presentadas al SRI
    /// (<c>PendingSriResolution</c>) nunca verificadas o verificadas antes de <paramref name="checkedBeforeUtc"/>,
    /// las más antiguas primero. Solo identificadores.
    /// </summary>
    Task<IReadOnlyList<(Guid TenantId, Guid CompanyId, Guid RequestId)>> GetDueForSriVerificationAsync(
        DateTime checkedBeforeUtc,
        int take,
        CancellationToken ct = default
    );
}
