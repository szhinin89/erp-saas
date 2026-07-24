using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class CompanyUserPreferencesRepository : ICompanyUserPreferencesRepository
{
    private readonly ErpDbContext _db;

    public CompanyUserPreferencesRepository(ErpDbContext db)
    {
        _db = db;
    }

    // IgnoreQueryFilters(): se resuelve durante el propio flujo de login (para decidir
    // AskBranch vs. DirectToDefault), antes de que exista un ICurrentCompany ambiente
    // confiable — mismo criterio que CompanyUserBranchRepository/UserSessionRepository.
    public Task<CompanyUserPreferences?> GetByMembershipAsync(
        Guid companyUserMembershipId, CancellationToken cancellationToken = default)
        => _db.CompanyUserPreferences.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.CompanyUserMembershipId == companyUserMembershipId, cancellationToken);

    public Task<bool> ExistsAsync(
        Guid companyUserMembershipId, CancellationToken cancellationToken = default)
        => _db.CompanyUserPreferences.IgnoreQueryFilters()
            .AnyAsync(x => x.CompanyUserMembershipId == companyUserMembershipId, cancellationToken);

    public Task AddAsync(CompanyUserPreferences entity, CancellationToken cancellationToken = default)
        => _db.CompanyUserPreferences.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
