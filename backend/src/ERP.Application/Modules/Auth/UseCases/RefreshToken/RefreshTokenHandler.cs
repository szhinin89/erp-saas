using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.RefreshToken;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly ICompanyRepository _companyRepository;

    public RefreshTokenHandler(
        IRefreshTokenService refreshTokenService,
        IAccessRepository accessRepository,
        ITenantRepository TenantRepository,
        IAccessTokenService accessTokenService,
        ICompanyRepository companyRepository)
    {
        _refreshTokenService = refreshTokenService;
        _accessRepository = accessRepository;
        _tenantRepository = TenantRepository;
        _accessTokenService = accessTokenService;
        _companyRepository = companyRepository;
    }

    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var v = await _refreshTokenService.ValidateAndRotateAsync(command.RawRefreshToken, cancellationToken);
        if (!v.IsValid)
        {
            if (v.IsRateLimited)
                return Result<AuthResponseDto>.Failure(
                    v.Error ?? "Demasiados intentos de renovación.", ApiResponseCodes.Common.RateLimited);
            return Result<AuthResponseDto>.Failure(v.Error ?? "Refresh token inválido.");
        }

        if (v.UserType != RefreshUserType.Identity)
            return Result<AuthResponseDto>.Failure("Tipo de sesión no soportado. Inicie sesión nuevamente.");

        var user = await _accessRepository.GetUserByIdAsync(v.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario no válido.");

        var tenant = await _tenantRepository.GetByIdAsync(v.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Tenant no encontrado o inactivo.");

        Guid? companyId = v.CompanyId;
        CompanyUserMembership? membership = null;
        ERP.Domain.Modules.Company.Entities.Company? resolvedCompany = null;

        if (companyId is Guid cid && cid != Guid.Empty)
        {
            resolvedCompany = await _companyRepository.GetByIdForTenantAsync(cid, v.TenantId, cancellationToken);
            if (resolvedCompany is null)
                return Result<AuthResponseDto>.Failure("Empresa no válida para el tenant.");

            membership = await _accessRepository.GetCompanyUserMembershipAsync(resolvedCompany.Id, user.Id, cancellationToken);
            if (membership is null || !membership.IsActive)
                return Result<AuthResponseDto>.Failure("Membresía no activa para la empresa.");
        }
        else
        {
            var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(user.Id, cancellationToken);
            var companies = await _companyRepository.GetByIdsAsync(memberships.Select(m => m.CompanyId).ToList(), cancellationToken);
            var inTenant = companies.Where(c => c.TenantId == v.TenantId).ToList();

            if (inTenant.Count == 1)
            {
                resolvedCompany = inTenant[0];
                companyId = resolvedCompany.Id;
                membership = memberships.First(m => m.CompanyId == companyId);
            }
            else if (inTenant.Count == 0)
            {
                return Result<AuthResponseDto>.Failure("Sin acceso a empresas en este tenant.");
            }
            else
            {
                // N companies: emit pending-company token — SwitchCompany will issue the final one.
                const string pendingCompanyRole = SecurityRoles.User;
                var accessTokenPartial = _accessTokenService.GenerateSessionToken(
                    user, v.TenantId, pendingCompanyRole);

                return Result<AuthResponseDto>.Success(new AuthResponseDto(
                    user.Id, user.FullName, user.Username, user.Email?.Value,
                    pendingCompanyRole, v.TenantId, accessTokenPartial)
                {
                    CompanyId = null,
                    RequiresCompanySelection = true,
                    RefreshToken = v.NewToken,
                    RefreshTokenExpiry = v.NewExpiry,
                });
            }
        }

        membership ??= await _accessRepository.GetCompanyUserMembershipAsync(companyId!.Value, user.Id, cancellationToken);
        if (membership is null || !membership.IsActive)
            return Result<AuthResponseDto>.Failure("Membresía no encontrada.");

        var accessToken = _accessTokenService.GenerateSessionToken(
            user, v.TenantId, membership.Role);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id, user.FullName, user.Username, user.Email?.Value,
            membership.Role, v.TenantId, accessToken)
        {
            CompanyId = companyId,
            OnboardingCompleted = resolvedCompany?.OnboardingCompleted ?? false,
            OperationalStatus = resolvedCompany?.OperationalStatus,
            RefreshToken = v.NewToken,
            RefreshTokenExpiry = v.NewExpiry,
        });
    }
}

