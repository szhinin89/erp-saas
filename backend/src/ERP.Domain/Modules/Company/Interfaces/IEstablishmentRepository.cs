using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IEstablishmentRepository
{
    Task<IReadOnlyList<Establishment>> GetByBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<Establishment>> GetActiveByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Devuelve establecimientos filtrados con navegación Branch e EmissionPoints incluidos (para el listado de la pantalla independiente).</summary>
    Task<IReadOnlyList<Establishment>> GetFilteredAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        bool? isActive,
        string? search,
        CancellationToken cancellationToken = default
    );

    Task<Establishment?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// ZH-AUTH-MASTERDATA-REPOSITORY-COMPANY-SCOPE-07A — defensa en profundidad explícita: además
    /// del filtro global de EF (<c>ICompanyScopedEntity</c>, ya fail-closed), valida <paramref
    /// name="companyId"/> en el propio predicado de la consulta, para no depender únicamente del
    /// filtro global como única barrera. Preferir este método sobre <see cref="GetByIdAsync"/> en
    /// todo caller que ya tenga <c>ICurrentCompany.CompanyId</c> disponible.
    /// </summary>
    Task<Establishment?> GetByIdForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default
    );
    Task<Establishment?> GetMainByBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    );
    Task<Establishment?> GetMainByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Verifica si ya existe un establecimiento con ese código en la empresa (único a nivel company según SRI).</summary>
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken cancellationToken = default
    );

    /// <summary>Retorna true si el establecimiento tiene al menos un EmissionPoint activo.</summary>
    Task<bool> HasActiveEmissionPointsAsync(
        Guid tenantId,
        Guid establishmentId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Establishment establishment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// CONFIG-FOUNDATION-P2-01: desmarca todo establecimiento IsMain=true de la empresa distinto
    /// de <paramref name="exceptEstablishmentId"/> — antes de esta entrega, UpdateEstablishmentCommandHandler
    /// llamaba a Establishment.SetMain(true, ...) sin desmarcar el establecimiento principal
    /// anterior, lo que habría violado uq_establishment_tenant_company_main (CONFIG-FOUNDATION-P0-01)
    /// con una excepción cruda en vez de un flip controlado — mismo patrón ya usado por
    /// IBranchRepository.ClearMainBranchExceptAsync / IEmissionPointRepository.ClearDefaultExceptAsync.
    /// Devuelve los Id desmarcados (para auditoría).
    /// </summary>
    Task<IReadOnlyList<Guid>> ClearMainExceptAsync(
        Guid tenantId,
        Guid companyId,
        Guid? exceptEstablishmentId,
        Guid updatedBy,
        CancellationToken cancellationToken = default
    );
}
