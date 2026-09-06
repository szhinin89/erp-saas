using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

public interface ICompanyUserBranchRepository
{
    Task<IReadOnlyList<CompanyUserBranch>> GetByMembershipAsync(
        Guid companyUserMembershipId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Conteo agregado de sucursales activas asignadas por membership (batch, evita N+1 en listados multiempresa).</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountActiveByMembershipIdsAsync(
        IReadOnlyCollection<Guid> membershipIds,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsAsync(
        Guid companyUserMembershipId,
        Guid branchId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(CompanyUserBranch entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// No reatacha ni fuerza EntityState — la entidad debe haberse obtenido previamente vía
    /// GetByMembershipAsync (misma instancia de ErpDbContext) y mutado con sus métodos de
    /// dominio. Existe por simetría de contrato con el resto de repositorios del proyecto.
    /// </summary>
    Task UpdateAsync(CompanyUserBranch entity, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
