using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IPhysicalDocumentAuthorizationRepository
{
    /// <summary>Autorizaciones de la empresa (activas e inactivas), más recientes primero.</summary>
    Task<IReadOnlyList<PhysicalDocumentAuthorization>> GetAllByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Autorizaciones activas del punto de emisión para el tipo documental.</summary>
    Task<IReadOnlyList<PhysicalDocumentAuthorization>> GetActiveAsync(
        Guid tenantId,
        Guid companyId,
        Guid emissionPointId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    );

    Task<PhysicalDocumentAuthorization?> GetByIdForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        PhysicalDocumentAuthorization authorization,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
