using System.Security.Claims;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.OperateCompany;

/// <summary>
/// Ver <see cref="OperateCompanyCommand"/>. Mismo esqueleto que <c>SwitchCompanyHandler</c>, pero
/// el caller es un admin global (tenant_id == Guid.Empty, sin CompanyUserMembership) en vez de un
/// usuario de una empresa — por eso resuelve la empresa/tenant/sucursal vía las variantes que
/// bypassan el filtro de tenant ambiente (<c>AsPlatformQuery</c>), en vez de depender de
/// <see cref="ICurrentTenant"/>/membership.
/// </summary>
public sealed class OperateCompanyHandler
    : IRequestHandler<OperateCompanyCommand, Result<AuthResponseDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public OperateCompanyHandler(
        IAccessRepository accessRepository,
        ICompanyRepository companyRepository,
        ITenantRepository tenantRepository,
        IBranchRepository branchRepository,
        IAccessTokenService tokenService,
        IRefreshTokenService refreshTokenService,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant
    )
    {
        _accessRepository = accessRepository;
        _companyRepository = companyRepository;
        _tenantRepository = tenantRepository;
        _branchRepository = branchRepository;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        OperateCompanyCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated)
            return Result<AuthResponseDto>.Failure("No autenticado.");

        if (_currentTenant.TenantId != Guid.Empty)
            return Result<AuthResponseDto>.Failure(
                "Esta operación requiere una sesión de administrador global."
            );

        var user = await _accessRepository.GetUserByIdAsync(_currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario no válido.");

        var globalRole = await _accessRepository.GetActiveGlobalUserRoleAsync(
            user.Id,
            SecurityRoles.Admin,
            cancellationToken
        );
        if (globalRole is null)
            return Result<AuthResponseDto>.Failure("No autorizado como administrador global.");

        var company = await _companyRepository.GetTrackedByIdForIntegrationAsync(
            command.CompanyId,
            cancellationToken
        );
        if (company is null || !company.IsActive)
            return Result<AuthResponseDto>.Failure("Empresa no encontrada o inactiva.");

        var tenant = await _tenantRepository.GetByIdAsync(company.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Tenant destino inactivo.");

        var branches = await _branchRepository.GetByCompanyAsync(
            company.TenantId,
            company.Id,
            activeFilter: true,
            search: null,
            cancellationToken
        );
        var mainBranches = branches.Where(b => b.IsMainBranch).ToList();
        if (mainBranches.Count != 1)
            return Result<AuthResponseDto>.Failure(
                "La empresa no tiene una sucursal principal resoluble."
            );

        var token = _tokenService.GenerateSessionToken(
            user,
            company.TenantId,
            SecurityRoles.Admin,
            new Claim[]
            {
                new("operator_mode", "true"),
                new("global_admin_user_id", user.Id.ToString()),
            }
        );

        var (refreshToken, refreshTokenExpiry) = await _refreshTokenService.CreateAsync(
            user.Id,
            company.TenantId,
            company.Id,
            RefreshUserType.Identity,
            cancellationToken,
            isOperatorSession: true,
            globalAdminUserId: user.Id
        );

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                user.Id,
                user.FullName,
                user.Username,
                user.Email?.Value,
                SecurityRoles.Admin,
                company.TenantId,
                token
            )
            {
                CompanyId = company.Id,
                RequiresCompanySelection = false,
                OnboardingCompleted = company.OnboardingCompleted,
                OperationalStatus = company.OperationalStatus,
                RefreshToken = refreshToken,
                RefreshTokenExpiry = refreshTokenExpiry,
                OperatorMode = true,
                GlobalAdminUserId = user.Id,
            }
        );
    }
}
