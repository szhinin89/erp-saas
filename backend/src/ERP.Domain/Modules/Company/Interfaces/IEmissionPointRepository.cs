using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IEmissionPointRepository
{
    Task<IReadOnlyList<EmissionPoint>> GetByEstablishmentAsync(
        Guid tenantId,
        Guid establishmentId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Puntos de emisión activos cuyo Establecimiento pertenece a la Sucursal indicada — para el selector de Caja (Sucursal → Punto de Emisión).</summary>
    Task<IReadOnlyList<EmissionPoint>> GetActiveByBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Lista todos los puntos de emisión de la empresa con su Establishment cargado (para proyectar en el handler).</summary>
    Task<IReadOnlyList<EmissionPoint>> GetAllByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        bool? activeFilter,
        string? search,
        CancellationToken cancellationToken = default
    );
    Task<EmissionPoint?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
    Task<EmissionPoint?> GetDefaultForBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    );
    Task<EmissionPoint?> GetDefaultForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );
    /// <summary>CONFIG-FOUNDATION-P2-01: devuelve los Id de los puntos de emisión desmarcados (para auditoría).</summary>
    Task<IReadOnlyList<Guid>> ClearDefaultExceptAsync(
        Guid tenantId,
        Guid establishmentId,
        Guid? exceptId,
        Guid updatedBy,
        CancellationToken cancellationToken = default
    );
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid establishmentId,
        string code,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(EmissionPoint emissionPoint, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
