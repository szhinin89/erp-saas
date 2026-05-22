using MediatR;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Auth.UseCases.RefreshToken;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccessRepository _accessRepository;
    private readonly ISubscriberRepository _tenantRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ICompanyRepository _companyRepository;

    public RefreshTokenHandler(
        IRefreshTokenService refreshTokenService,
        IAccessRepository accessRepository,
        ISubscriberRepository tenantRepository,
        IAccessTokenService accessTokenService,
        ISessionModulesResolver sessionModules,
        ICompanyRepository companyRepository)
    {
        _refreshTokenService = refreshTokenService;
        _accessRepository = accessRepository;
        _tenantRepository = tenantRepository;
        _accessTokenService = accessTokenService;
        _sessionModules = sessionModules;
        _companyRepository = companyRepository;
    }

    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var v = await _refreshTokenService.ValidateAndRotateAsync(command.RawRefreshToken, ct);
        if (!v.IsValid)
        {
            if (v.IsRateLimited)
                return Result<AuthResponseDto>.Failure(
                    v.Error ?? "Demasiados intentos de renovación.",
                    "rate_limited");
            return Result<AuthResponseDto>.Failure(v.Error ?? "Refresh token inválido.");
        }

        if (v.UserType is RefreshUserType.Platform or RefreshUserType.SuperAdmin)
        {
            var platformUser = await _accessRepository.GetUserByIdAsync(v.UserId, ct);
            if (platformUser is null || !platformUser.IsPlatformSuperAdmin)
                return Result<AuthResponseDto>.Failure("Usuario no válido.");

            if (v.SubscriberId != Guid.Empty)
            {
                var impersonatedTenant = await _tenantRepository.GetByIdAsync(v.SubscriberId, ct);
                if (impersonatedTenant is null || !impersonatedTenant.IsActive)
                    return Result<AuthResponseDto>.Failure("Suscriptor no encontrado o inactivo.");

                var impersonatedCompanyId = v.CompanyId is Guid impersonatedCid && impersonatedCid != Guid.Empty
                    ? impersonatedCid
                    : Guid.Empty;
                var impersonatedAccessToken = impersonatedCompanyId != Guid.Empty
                    ? _accessTokenService.GenerateSessionToken(platformUser, v.SubscriberId, "SuperAdmin", impersonatedCompanyId)
                    : _accessTokenService.GenerateSessionToken(
                        platformUser.Id,
                        platformUser.Email.Value,
                        platformUser.FullName,
                        v.SubscriberId,
                        "SuperAdmin",
                        companyId: default,
                        userType: platformUser.UserType,
                        platformRole: platformUser.PlatformRole);

                var impersonatedModules = await _sessionModules.GetEnabledModuleKeysAsync(v.SubscriberId, ct);
                return Result<AuthResponseDto>.Success(new AuthResponseDto(
                    platformUser.Id, platformUser.FullName, platformUser.Email.Value,
                    "SuperAdmin", v.SubscriberId, impersonatedAccessToken,
                    impersonatedTenant.PlanCode, impersonatedModules)
                {
                    CompanyId = impersonatedCompanyId != Guid.Empty ? impersonatedCompanyId : null,
                    RefreshToken = v.NewToken,
                    RefreshTokenExpiry = v.NewExpiry,
                    UserType = platformUser.UserType.ToString(),
                    PlatformRole = platformUser.PlatformRole?.ToString(),
                });
            }

            var platformToken = _accessTokenService.GeneratePlatformSessionToken(platformUser);
            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                platformUser.Id, platformUser.FullName, platformUser.Email.Value,
                "SuperAdmin", Guid.Empty, platformToken,
                PlanCode: null,
                SubscriberSubscriptionCatalog.AllModuleKeys)
            {
                RefreshToken = v.NewToken,
                RefreshTokenExpiry = v.NewExpiry,
                UserType = platformUser.UserType.ToString(),
                PlatformRole = platformUser.PlatformRole?.ToString(),
            });
        }

        if (v.UserType == RefreshUserType.Legacy)
            return Result<AuthResponseDto>.Failure("Sesión legacy expirada. Inicie sesión nuevamente.");

        var user = await _accessRepository.GetUserByIdAsync(v.UserId, ct);
        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario no válido.");

        var tenant = await _tenantRepository.GetByIdAsync(v.SubscriberId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Suscriptor no encontrado o inactivo.");

        Guid? companyId = v.CompanyId;
        CompanyUserMembership? membership = null;

        if (companyId is Guid cid && cid != Guid.Empty)
        {
            var company = await _companyRepository.GetByIdForSubscriberAsync(cid, v.SubscriberId, ct);
            if (company is null)
                return Result<AuthResponseDto>.Failure("Empresa no válida para el suscriptor.");

            membership = await _accessRepository.GetCompanyUserMembershipAsync(company.Id, user.Id, ct);
            if (membership is null || !membership.IsActive)
                return Result<AuthResponseDto>.Failure("Membresía no activa para la empresa.");
        }
        else
        {
            var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(user.Id, ct);
            var companies = await _companyRepository.GetByIdsAsync(memberships.Select(m => m.CompanyId).ToList(), ct);
            var inSubscriber = companies.Where(c => c.SubscriberId == v.SubscriberId).ToList();
            if (inSubscriber.Count == 1)
            {
                companyId = inSubscriber[0].Id;
                membership = memberships.First(m => m.CompanyId == companyId);
            }
            else if (inSubscriber.Count == 0)
            {
                return Result<AuthResponseDto>.Failure("Sin acceso a empresas en este suscriptor.");
            }
            else
            {
                membership = memberships.First(m => m.CompanyId == inSubscriber[0].Id);
                var accessTokenPartial = _accessTokenService.GenerateSessionToken(
                    user, v.SubscriberId, membership.Role);
                var modulesPartial = await _sessionModules.GetEnabledModuleKeysAsync(v.SubscriberId, ct);
                return Result<AuthResponseDto>.Success(new AuthResponseDto(
                    user.Id, user.FullName, user.Email.Value,
                    membership.Role, v.SubscriberId, accessTokenPartial,
                    tenant.PlanCode, modulesPartial)
                {
                    CompanyId = null,
                    RefreshToken = v.NewToken,
                    RefreshTokenExpiry = v.NewExpiry,
                });
            }
        }

        membership ??= await _accessRepository.GetCompanyUserMembershipAsync(companyId!.Value, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<AuthResponseDto>.Failure("Membresía no encontrada.");

        var accessToken = _accessTokenService.GenerateSessionToken(
            user, v.SubscriberId, membership.Role, companyId!.Value);
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(v.SubscriberId, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id, user.FullName, user.Email.Value,
            membership.Role, v.SubscriberId, accessToken,
            tenant.PlanCode,
            modules)
        {
            CompanyId = companyId,
            RefreshToken = v.NewToken,
            RefreshTokenExpiry = v.NewExpiry,
        });
    }
}
