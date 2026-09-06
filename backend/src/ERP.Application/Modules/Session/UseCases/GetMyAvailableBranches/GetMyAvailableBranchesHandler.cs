using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Session.DTOs;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Session.UseCases.GetMyAvailableBranches;

/// <summary>
/// Lectura de "mis sucursales" — distinta de GetCompanyUserBranchesAdminQuery (que expone
/// todas las sucursales de la empresa con un flag de autorización, para el admin gestionando
/// a otro usuario). Esta query es self-service: solo la lista ya filtrada a
/// activas+autorizadas para el membership del usuario actual, más su preferencia de login.
///
/// ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: un admin global operando esta empresa (autorizado
/// por <see cref="IOperatorCompanyAccessPolicy"/> — misma política que
/// <c>ICompanyAccessGuard</c>/<c>IBranchAccessGuard</c>) recibe todas las sucursales activas de
/// la empresa, con preferencia AskBranch. Este chequeo corre ANTES de resolver membership y
/// gana incondicionalmente cuando aplica — no solo cuando no hay CompanyUserMembership. Caso
/// real que forzó esto: el mismo usuario puede tener una CompanyUserMembership propia en esta
/// empresa (p. ej. es también su Admin de empresa) cuya CompanyUserBranch fue revocada
/// (is_active=false) después; sin este orden, esa restricción puntual de la membership
/// eclipsaba por completo la capacidad de soporte global y el modal seguía mostrando "No tiene
/// sucursales asignadas" aunque el usuario sí pudiera operar en modo global.
/// </summary>
public sealed class GetMyAvailableBranchesHandler
    : IRequestHandler<GetMyAvailableBranchesQuery, Result<MyAvailableBranchesDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAccessRepository _accessRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;
    private readonly ICompanyUserPreferencesRepository _preferencesRepository;
    private readonly IOperatorCompanyAccessPolicy _operatorAccessPolicy;

    public GetMyAvailableBranchesHandler(
        ICurrentUser currentUser,
        ICurrentCompany currentCompany,
        ICurrentTenant currentTenant,
        IAccessRepository accessRepository,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        ICompanyUserPreferencesRepository preferencesRepository,
        IOperatorCompanyAccessPolicy operatorAccessPolicy
    )
    {
        _currentUser = currentUser;
        _currentCompany = currentCompany;
        _currentTenant = currentTenant;
        _accessRepository = accessRepository;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _preferencesRepository = preferencesRepository;
        _operatorAccessPolicy = operatorAccessPolicy;
    }

    public async Task<Result<MyAvailableBranchesDto>> Handle(
        GetMyAvailableBranchesQuery request,
        CancellationToken cancellationToken
    )
    {
        if (await _operatorAccessPolicy.IsAuthorizedOperatorAsync(cancellationToken))
            return await BuildForOperatorAsync(cancellationToken);

        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            _currentCompany.CompanyId,
            _currentUser.UserId,
            cancellationToken
        );

        if (membership is not null && membership.IsActive)
            return await BuildForMembershipAsync(membership, cancellationToken);

        return Result<MyAvailableBranchesDto>.Failure("No tiene acceso a esta empresa.");
    }

    private async Task<Result<MyAvailableBranchesDto>> BuildForMembershipAsync(
        CompanyUserMembership membership,
        CancellationToken cancellationToken
    )
    {
        var authorizations = await _companyUserBranchRepository.GetByMembershipAsync(
            membership.Id,
            cancellationToken
        );
        var authorizedBranchIds = authorizations
            .Where(a => a.IsActive)
            .Select(a => a.BranchId)
            .ToHashSet();

        var branches = (await GetActiveCompanyBranchesAsync(cancellationToken))
            .Where(b => authorizedBranchIds.Contains(b.Id))
            .OrderByDescending(b => b.IsMainBranch)
            .ThenBy(b => b.Name)
            .Select(b => new AvailableBranchOptionDto(b.Id, b.Name, b.IsMainBranch))
            .ToList();

        var preferences = await _preferencesRepository.GetByMembershipAsync(
            membership.Id,
            cancellationToken
        );
        var loginMode = preferences?.LoginMode.ToString() ?? nameof(CompanyUserLoginMode.AskBranch);

        return Result<MyAvailableBranchesDto>.Success(
            new MyAvailableBranchesDto(branches, loginMode, preferences?.DefaultBranchId)
        );
    }

    private async Task<Result<MyAvailableBranchesDto>> BuildForOperatorAsync(
        CancellationToken cancellationToken
    )
    {
        var branches = (await GetActiveCompanyBranchesAsync(cancellationToken))
            .OrderByDescending(b => b.IsMainBranch)
            .ThenBy(b => b.Name)
            .Select(b => new AvailableBranchOptionDto(b.Id, b.Name, b.IsMainBranch))
            .ToList();

        return Result<MyAvailableBranchesDto>.Success(
            new MyAvailableBranchesDto(branches, nameof(CompanyUserLoginMode.AskBranch), null)
        );
    }

    private Task<IReadOnlyList<ERP.Domain.Branches.Entities.Branch>> GetActiveCompanyBranchesAsync(
        CancellationToken cancellationToken
    ) =>
        _branchRepository.GetByCompanyAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            activeFilter: true,
            search: null,
            cancellationToken
        );
}
