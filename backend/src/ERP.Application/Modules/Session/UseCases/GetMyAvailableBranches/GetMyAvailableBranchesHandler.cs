using ERP.Application.Common;
using ERP.Application.Modules.Session.DTOs;
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

    public GetMyAvailableBranchesHandler(
        ICurrentUser currentUser,
        ICurrentCompany currentCompany,
        ICurrentTenant currentTenant,
        IAccessRepository accessRepository,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        ICompanyUserPreferencesRepository preferencesRepository
    )
    {
        _currentUser = currentUser;
        _currentCompany = currentCompany;
        _currentTenant = currentTenant;
        _accessRepository = accessRepository;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _preferencesRepository = preferencesRepository;
    }

    public async Task<Result<MyAvailableBranchesDto>> Handle(
        GetMyAvailableBranchesQuery request,
        CancellationToken cancellationToken
    )
    {
        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            _currentCompany.CompanyId,
            _currentUser.UserId,
            cancellationToken
        );
        if (membership is null || !membership.IsActive)
            return Result<MyAvailableBranchesDto>.Failure("No tiene acceso a esta empresa.");

        var authorizations = await _companyUserBranchRepository.GetByMembershipAsync(
            membership.Id,
            cancellationToken
        );
        var authorizedBranchIds = authorizations
            .Where(a => a.IsActive)
            .Select(a => a.BranchId)
            .ToHashSet();

        var branches = (
            await _branchRepository.GetAsync(
                _currentTenant.TenantId,
                activeFilter: true,
                search: null,
                cancellationToken
            )
        )
            .Where(b =>
                b.CompanyId == _currentCompany.CompanyId && authorizedBranchIds.Contains(b.Id)
            )
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
}
