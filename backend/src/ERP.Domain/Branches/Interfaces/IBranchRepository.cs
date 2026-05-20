using ERP.Domain.Branches.Entities;

namespace ERP.Domain.Branches.Interfaces;

public interface IBranchRepository
{
    Task AddAsync(Branch branch, CancellationToken ct = default);

    Task<IReadOnlyList<Branch>> GetAsync(
        Guid subscriberId,
        bool? activeFilter = true,
        string? search = null,
        CancellationToken ct = default);

    Task<Branch?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);

    Task ClearMainBranchExceptAsync(Guid subscriberId, Guid? exceptBranchId, Guid updatedBy, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
