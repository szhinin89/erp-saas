using ERP.Application.Access;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IMembershipAuthority"/>: delega a <see cref="IAccessRepository"/>
/// y <see cref="ICompanyRepository"/> sin lógica de negocio adicional.
/// </summary>
public sealed class MembershipAuthority : IMembershipAuthority
{
    private readonly IAccessRepository _access;
    private readonly ICompanyRepository _companies;

    public MembershipAuthority(IAccessRepository access, ICompanyRepository companies)
    {
        _access = access;
        _companies = companies;
    }

    public Task<CompanyUserMembership?> GetActiveMembershipAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _access.GetCompanyUserMembershipAsync(companyId, userId, cancellationToken)
            .ContinueWith(t => t.Result is { IsActive: true } m ? m : null, cancellationToken);

    public async Task<bool> HasActiveMembershipInTenantAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _access.GetActiveCompanyUserMembershipsForUserSystemAsync(userId, cancellationToken);
        if (memberships.Count == 0) return false;

        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var companies = await _companies.GetByIdsAsync(companyIds, cancellationToken);
        return companies.Any(c => c.TenantId == tenantId);
    }
}
