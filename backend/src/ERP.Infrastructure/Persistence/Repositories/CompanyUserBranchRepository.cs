using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class CompanyUserBranchRepository : ICompanyUserBranchRepository
{
    private readonly ErpDbContext _db;

    public CompanyUserBranchRepository(ErpDbContext db)
    {
        _db = db;
    }

    // IgnoreQueryFilters(): se invoca durante el flujo de login/selección de sucursal,
    // antes de que exista un ICurrentCompany ambiente confiable — mismo criterio que
    // AccessRepository aplica a CompanyUserMembership.
    public async Task<IReadOnlyList<CompanyUserBranch>> GetByMembershipAsync(
        Guid companyUserMembershipId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .CompanyUserBranches.AsPlatformQuery()
            .Where(x => x.CompanyUserMembershipId == companyUserMembershipId)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        Guid companyUserMembershipId,
        Guid branchId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .CompanyUserBranches.AsPlatformQuery()
            .AnyAsync(
                x =>
                    x.CompanyUserMembershipId == companyUserMembershipId
                    && x.BranchId == branchId
                    && x.IsActive,
                cancellationToken
            );

    public Task AddAsync(CompanyUserBranch entity, CancellationToken cancellationToken = default) =>
        _db.CompanyUserBranches.AddAsync(entity, cancellationToken).AsTask();

    // No-op intencional: mismo criterio que UserSessionRepository.UpdateAsync — la entidad
    // debe llegar ya tracked (obtenida vía GetByMembershipAsync en este mismo DbContext).
    public Task UpdateAsync(
        CompanyUserBranch entity,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
