using ERP.Domain.Branches.Entities;

namespace ERP.Domain.Branches.Interfaces;

public interface IBranchRepository
{
    Task AddAsync(Branch branch, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Branch>> GetAsync(
        Guid tenantId,
        bool? activeFilter = true,
        string? search = null,
        CancellationToken cancellationToken = default
    );

    Task<Branch?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>CONFIG-FOUNDATION-P2-01: devuelve los Id de las sucursales desmarcadas (para auditoría).</summary>
    Task<IReadOnlyList<Guid>> ClearMainBranchExceptAsync(
        Guid tenantId,
        Guid? exceptBranchId,
        Guid updatedBy,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
