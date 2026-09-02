using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.GetCompanyUserBranchesAdmin;

/// <summary>
/// Fase I-B. Mismo criterio de aislamiento que GetCompanyUserPreferencesAdminHandler (Fase F):
/// mismo mensaje para "membresía inexistente" y "pertenece a otra empresa" — evita que un
/// administrador de otra empresa pueda confirmar por diferencia de respuesta que un
/// CompanyUserId de otra empresa existe.
/// IBranchRepository mantiene métodos tenant-only para login/pre-login, pero esta lectura
/// administrativa siempre debe pedir sucursales por empresa explícita.
/// </summary>
public sealed class GetCompanyUserBranchesAdminHandler
    : IRequestHandler<GetCompanyUserBranchesAdminQuery, Result<CompanyUserBranchesAdminDto?>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentTenant _currentTenant;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;

    public GetCompanyUserBranchesAdminHandler(
        IAccessRepository accessRepository,
        ICurrentCompany currentCompany,
        ICurrentTenant currentTenant,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository
    )
    {
        _accessRepository = accessRepository;
        _currentCompany = currentCompany;
        _currentTenant = currentTenant;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
    }

    public async Task<Result<CompanyUserBranchesAdminDto?>> Handle(
        GetCompanyUserBranchesAdminQuery request,
        CancellationToken cancellationToken
    )
    {
        var membership = await _accessRepository.GetCompanyUserMembershipByIdAsync(
            request.CompanyUserId,
            cancellationToken
        );
        if (membership is null || membership.CompanyId != _currentCompany.CompanyId)
            return Result<CompanyUserBranchesAdminDto?>.NotFound(
                "Usuario de empresa no encontrado."
            );

        var companyBranches = (
            await _branchRepository.GetByCompanyAsync(
                _currentTenant.TenantId,
                membership.CompanyId,
                activeFilter: true,
                search: null,
                cancellationToken: cancellationToken
            )
        )
            .ToList();

        var authorizations = await _companyUserBranchRepository.GetByMembershipAsync(
            membership.Id,
            cancellationToken
        );
        var authorizedBranchIds = authorizations
            .Where(a => a.IsActive)
            .Select(a => a.BranchId)
            .ToHashSet();

        var options = companyBranches
            .Select(b => new CompanyUserBranchOptionDto(
                b.Id,
                b.Name,
                authorizedBranchIds.Contains(b.Id)
            ))
            .ToList();

        return Result<CompanyUserBranchesAdminDto?>.Success(
            new CompanyUserBranchesAdminDto(membership.Id, options)
        );
    }
}
