using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IEstablishmentRepository
{
    Task<IReadOnlyList<Establishment>> GetByBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Establishment>> GetActiveByCompanyAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Devuelve establecimientos filtrados con navegación Branch e EmissionPoints incluidos (para el listado de la pantalla independiente).</summary>
    Task<IReadOnlyList<Establishment>> GetFilteredAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        bool? isActive,
        string? search,
        CancellationToken cancellationToken = default);

    Task<Establishment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<Establishment?> GetMainByBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);
    Task<Establishment?> GetMainByCompanyAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Verifica si ya existe un establecimiento con ese código en la empresa (único a nivel company según SRI).</summary>
    Task<bool> ExistsAsync(Guid tenantId, Guid companyId, string code, CancellationToken cancellationToken = default);

    /// <summary>Retorna true si el establecimiento tiene al menos un EmissionPoint activo.</summary>
    Task<bool> HasActiveEmissionPointsAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken = default);

    Task AddAsync(Establishment establishment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
