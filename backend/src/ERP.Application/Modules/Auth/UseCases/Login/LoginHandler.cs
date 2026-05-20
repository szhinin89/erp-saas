using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.Login;

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ISubscriberRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IJwtService _jwtService;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ICompanyProvisioningService _companyProvisioning;

    public LoginHandler(
        IUserRepository userRepository,
        ISubscriberRepository tenantRepository,
        ICompanyRepository companyRepository,
        IJwtService jwtService,
        IDeploymentFeatureFlags deployment,
        IAccessRepository accessRepository,
        IAccessTokenService accessTokenService,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        ISessionModulesResolver sessionModules,
        ICompanyProvisioningService companyProvisioning)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _companyRepository = companyRepository;
        _jwtService = jwtService;
        _deployment = deployment;
        _accessRepository = accessRepository;
        _accessTokenService = accessTokenService;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _sessionModules = sessionModules;
        _companyProvisioning = companyProvisioning;
    }

    public async Task<Result<AuthResponseDto>> Handle(LoginCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim();

        var superAdmin = await _userRepository.GetSingleSuperAdminByEmailAsync(email, ct);
        if (superAdmin is not null)
        {
            if (!_deployment.IsSuperAdminPanelEnabled)
                return Result<AuthResponseDto>.Failure(DeploymentAuthMessages.SuperAdminPanelDisabled);

            if (!superAdmin.IsActive)
                return Result<AuthResponseDto>.Failure("Usuario inactivo.");

            var superPasswordValid = _passwordHasher.VerifyPassword(command.Password, superAdmin.PasswordHash);
            if (!superPasswordValid)
                return Result<AuthResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

            var globalToken = _jwtService.GenerateToken(superAdmin, Guid.Empty);
            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                superAdmin.Id,
                superAdmin.FullName,
                superAdmin.Email.Value,
                superAdmin.Role,
                Guid.Empty,
                globalToken,
                PlanCode: null,
                EnabledModules: SubscriberSubscriptionCatalog.AllModuleKeys));
        }

        var identityUser = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (identityUser is not null)
        {
            if (!identityUser.IsActive)
                return Result<AuthResponseDto>.Failure("Usuario inactivo.");

            if (!_passwordHasher.VerifyPassword(command.Password, identityUser.PasswordHash))
                return Result<AuthResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

            var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(identityUser.Id, ct);
            if (memberships.Count == 0)
                return Result<AuthResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

            var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
            var companies = await _companyRepository.GetByIdsAsync(companyIds, ct);
            var companyById = companies.ToDictionary(c => c.Id);
            var membershipByCompany = memberships.ToDictionary(m => m.CompanyId);

            var subscriberGroups = companies
                .GroupBy(c => c.SubscriberId)
                .ToList();

            if (subscriberGroups.Count > 1)
                return Result<AuthResponseDto>.Failure("Tu usuario está asociado a múltiples suscriptores. Usa el flujo de selección de suscriptor.");

            var subscriberId = subscriberGroups[0].Key;
            var subscriber = await _tenantRepository.GetByIdAsync(subscriberId, ct);
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

        var subscribers = await _tenantRepository.GetAllAsync(ct);
        var matches = new List<Domain.Auth.Entities.User>();

        foreach (var tenant in subscribers.Where(t => t.IsActive))
        {
            var user = await _userRepository.GetByEmailSystemAsync(email, tenant.Id, ct);
            if (user is not null)
                matches.Add(user);
        }

        if (matches.Count == 0)
            return Result<AuthResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

        if (matches.Count > 1)
            return Result<AuthResponseDto>.Failure("Tu usuario está asociado a múltiples empresas. Comunícate con el administrador.");

        var single = matches[0];

        if (!single.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        if (!_passwordHasher.VerifyPassword(command.Password, single.PasswordHash))
            return Result<AuthResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        var token = _jwtService.GenerateToken(single);
        var tenantEntity = await _tenantRepository.GetByIdAsync(single.SubscriberId, ct);
        var (legacyRefresh, legacyRefreshExpiry) = await _refreshTokenService.CreateAsync(
            single.Id, single.SubscriberId, null, RefreshUserType.Legacy, ct);

        var legacyModules = tenantEntity is null
            ? Array.Empty<string>()
            : await _sessionModules.GetEnabledModuleKeysAsync(single.SubscriberId, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            single.Id,
            single.FullName,
            single.Email.Value,
            single.Role,
            single.SubscriberId,
            token,
            tenantEntity?.PlanCode,
            legacyModules)
        {
            RefreshToken = legacyRefresh,
            RefreshTokenExpiry = legacyRefreshExpiry,
        });
    }
}
