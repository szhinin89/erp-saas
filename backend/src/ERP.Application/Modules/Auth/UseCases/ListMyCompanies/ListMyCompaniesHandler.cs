using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.ListMyCompanies;

public sealed class ListMyCompaniesHandler
    : IRequestHandler<ListMyCompaniesQuery, Result<IReadOnlyList<AccessibleCompanyDto>>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public ListMyCompaniesHandler(
        IAccessRepository accessRepository,
        ICompanyRepository companyRepository,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant
    )
    {
        _accessRepository = accessRepository;
        _companyRepository = companyRepository;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<AccessibleCompanyDto>>> Handle(
        ListMyCompaniesQuery request,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated)
            return Result<IReadOnlyList<AccessibleCompanyDto>>.Failure("No autenticado.");

        var tenantId = _currentTenant.TenantId;
        if (tenantId == Guid.Empty)
            return Result<IReadOnlyList<AccessibleCompanyDto>>.Failure(
                "Contexto de tenant no establecido."
            );

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(
            _currentUser.UserId,
            cancellationToken
        );
        if (memberships.Count == 0)
            return Result<IReadOnlyList<AccessibleCompanyDto>>.Success(
                Array.Empty<AccessibleCompanyDto>()
            );

        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetByIdsAsync(companyIds, cancellationToken);
        var membershipByCompany = memberships.ToDictionary(m => m.CompanyId);

        // Batch: conteo de sucursales activas por empresa (total) y por membership (asignadas
        // al usuario), en una sola query cada uno — evita N+1 sobre las N empresas del usuario.
        var totalBranchCounts = await _branchRepository.CountActiveByCompanyIdsAsync(
            tenantId,
            companyIds,
            cancellationToken
        );
        var membershipIds = memberships.Select(m => m.Id).Distinct().ToList();
        var assignedBranchCounts = await _companyUserBranchRepository.CountActiveByMembershipIdsAsync(
            membershipIds,
            cancellationToken
        );

        var items = companies
            .Where(c => c.TenantId == tenantId)
            .Select(c =>
            {
                var m = membershipByCompany[c.Id];
                var totalBranches = totalBranchCounts.GetValueOrDefault(c.Id);
                // Admin bypassa los checks de permisos/asignación (SecurityRoles.Admin) — para
                // este rol la cantidad "asignada" es la totalidad de sucursales activas de la
                // empresa, no las filas explícitas de CompanyUserBranch.
                var assignedBranches = string.Equals(
                    m.Role,
                    SecurityRoles.Admin,
                    StringComparison.OrdinalIgnoreCase
                )
                    ? totalBranches
                    : assignedBranchCounts.GetValueOrDefault(m.Id);

                return new AccessibleCompanyDto(
                    c.Id,
                    c.TenantId,
                    c.LegalName,
                    c.TradeName ?? c.LegalName,
                    c.TaxIdentificationNumber,
                    m.Role,
                    c.IsActive,
                    c.OperationalStatus.ToString(),
                    c.TaxRegime?.Name,
                    c.IsAccountingReq,
                    assignedBranches,
                    totalBranches
                );
            })
            .OrderBy(x => x.LegalName)
            .ToList();

        return Result<IReadOnlyList<AccessibleCompanyDto>>.Success(items);
    }
}
