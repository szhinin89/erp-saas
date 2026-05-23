using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.Login;

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IDeploymentFeatureFlags _deployment;

    public LoginHandler(
        ISubscriberRepository subscriberRepository,
        ICompanyRepository companyRepository,
        IAccessRepository accessRepository,
        IAccessTokenService accessTokenService,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        ISessionModulesResolver sessionModules,
        ICompanyProvisioningService companyProvisioning,
        IDeploymentFeatureFlags deployment)
    {
        _subscriberRepository = subscriberRepository;
        _companyRepository = companyRepository;
        _accessRepository = accessRepository;
        _accessTokenService = accessTokenService;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _sessionModules = sessionModules;
        _companyProvisioning = companyProvisioning;
        _deployment = deployment;
    }

    public async Task<Result<AuthResponseDto>> Handle(LoginCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim();

        var identityUser = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (identityUser is null)
            return Result<AuthResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

        if (identityUser.IsPlatformSuperAdmin)
            return await LoginPlatformSuperAdminAsync(identityUser, command.Password, ct);

        if (!identityUser.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        if (identityUser.RequirePasswordReset)
            return Result<AuthResponseDto>.Failure("Debe restablecer su contraseña antes de iniciar sesión.");

        if (!_passwordHasher.VerifyPassword(command.Password, identityUser.PasswordHash))
            return Result<AuthResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(identityUser.Id, ct);
        if (memberships.Count == 0)
            return Result<AuthResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetByIdsAsync(companyIds, ct);
        var membershipByCompany = memberships.ToDictionary(m => m.CompanyId);

        var subscriberGroups = companies
            .GroupBy(c => c.SubscriberId)
            .ToList();

        if (subscriberGroups.Count > 1)
            return Result<AuthResponseDto>.Failure("Tu usuario está asociado a múltiples suscriptores. Usa el flujo de selección de suscriptor.");

        if (subscriberGroups.Count == 0)
            return Result<AuthResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

        var subscriberId = subscriberGroups[0].Key;
        var subscriber = await _subscriberRepository.GetByIdAsync(subscriberId, ct);
        if (subscriber is null || !subscriber.IsActive)
            return Result<AuthResponseDto>.Failure("Suscriptor inactivo o no encontrado.");

        var companiesInSubscriber = subscriberGroups[0].ToList();
        if (companiesInSubscriber.Count > 1)
        {
            var tokenWithoutCompany = _accessTokenService.GenerateSessionToken(
                identityUser, subscriberId, membershipByCompany[companiesInSubscriber[0].Id].Role);
            var (refreshMulti, refreshExpiryMulti) = await _refreshTokenService.CreateAsync(
                identityUser.Id, subscriberId, null, RefreshUserType.Identity, ct);
            var modulesMulti = await _sessionModules.GetEnabledModuleKeysAsync(subscriberId, ct);

            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                identityUser.Id,
                identityUser.FullName,
                identityUser.Email.Value,
                membershipByCompany[companiesInSubscriber[0].Id].Role,
                subscriberId,
                tokenWithoutCompany,
                subscriber.PlanCode,
                modulesMulti)
            {
                CompanyId = null,
                RefreshToken = refreshMulti,
                RefreshTokenExpiry = refreshExpiryMulti,
            });
        }

        var company = companiesInSubscriber[0];
        if (!membershipByCompany.TryGetValue(company.Id, out var membership))
            return Result<AuthResponseDto>.Failure("Membresía no encontrada.");

        await _companyProvisioning.EnsureDefaultCompanyAsync(subscriber, ct);

        var identityToken = _accessTokenService.GenerateSessionToken(
            identityUser, subscriberId, membership.Role, company.Id);
        var (identityRefresh, identityRefreshExpiry) = await _refreshTokenService.CreateAsync(
            identityUser.Id, subscriberId, company.Id, RefreshUserType.Identity, ct);
        var identityModules = await _sessionModules.GetEnabledModuleKeysAsync(subscriberId, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            identityUser.Id,
            identityUser.FullName,
            identityUser.Email.Value,
            membership.Role,
            subscriberId,
            identityToken,
            subscriber.PlanCode,
            identityModules)
        {
            CompanyId = company.Id,
            RefreshToken = identityRefresh,
            RefreshTokenExpiry = identityRefreshExpiry,
        });
    }

    private async Task<Result<AuthResponseDto>> LoginPlatformSuperAdminAsync(
        IdentityUser platformUser,
        string password,
        CancellationToken ct)
    {
        if (!_deployment.IsSuperAdminPanelEnabled)
            return Result<AuthResponseDto>.Failure(DeploymentAuthMessages.SuperAdminPanelDisabled);

        if (!platformUser.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        if (platformUser.RequirePasswordReset)
            return Result<AuthResponseDto>.Failure("Debe restablecer su contraseña antes de iniciar sesión.");

        if (!_passwordHasher.VerifyPassword(password, platformUser.PasswordHash))
            return Result<AuthResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        var token = _accessTokenService.GeneratePlatformSessionToken(platformUser);
        var (refresh, refreshExpiry) = await _refreshTokenService.CreateAsync(
            platformUser.Id, Guid.Empty, null, RefreshUserType.Platform, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            platformUser.Id,
            platformUser.FullName,
            platformUser.Email.Value,
            "SuperAdmin",
            Guid.Empty,
            token,
            PlanCode: null,
            EnabledModules: SubscriberSubscriptionCatalog.AllModuleKeys)
        {
            RefreshToken = refresh,
            RefreshTokenExpiry = refreshExpiry,
            UserType = platformUser.UserType.ToString(),
            PlatformRole = platformUser.PlatformRole?.ToString(),
        });
    }
}
