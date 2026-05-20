using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

using MediatR;

namespace ERP.Application.Auth.UseCases.Login;

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository     _userRepository;
    private readonly ITenantRepository   _tenantRepository;
    private readonly IJwtService         _jwtService;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IAccessRepository   _accessRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IPasswordHasher     _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ISessionModulesResolver _sessionModules;

    public LoginHandler(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IJwtService jwtService,
        IDeploymentFeatureFlags deployment,
        IAccessRepository accessRepository,
        IAccessTokenService accessTokenService,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        ISessionModulesResolver sessionModules)
    {
        _userRepository      = userRepository;
        _tenantRepository    = tenantRepository;
        _jwtService          = jwtService;
        _deployment          = deployment;
        _accessRepository    = accessRepository;
        _accessTokenService  = accessTokenService;
        _passwordHasher      = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _sessionModules      = sessionModules;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        LoginCommand command,
        CancellationToken ct)
    {
        var email = command.Email.Trim();

        // SuperAdmin auto-detection: allow global login (no tenant context).
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
            // SuperAdmin no emite refresh token (sesión corta, control global)
            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                superAdmin.Id,
                superAdmin.FullName,
                superAdmin.Email.Value,
                superAdmin.Role,
                Guid.Empty,
                globalToken,
                PlanCode: null,
                EnabledModules: TenantSubscriptionCatalog.AllModuleKeys));
        }

        // Usuarios solo en identity_users + memberships (p. ej. Admin creado al dar de alta una empresa).
        // El escaneo legacy en users por tenant no los encuentra; sin este paso /api/auth/login fallaba tras bootstrap.
        var identityUser = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (identityUser is not null)
        {
            if (!identityUser.IsActive)
                return Result<AuthResponseDto>.Failure("Usuario inactivo.");

            var identityPasswordOk = _passwordHasher.VerifyPassword(command.Password, identityUser.PasswordHash);
            if (!identityPasswordOk)
                return Result<AuthResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

            var memberships = await _accessRepository.GetActiveMembershipsForUserSystemAsync(identityUser.Id, ct);
            if (memberships.Count == 0)
                return Result<AuthResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

            if (memberships.Count > 1)
                return Result<AuthResponseDto>.Failure("Tu usuario está asociado a múltiples empresas. Comunícate con el administrador.");

            var membership = memberships[0];
            var identityTenant = await _tenantRepository.GetByIdAsync(membership.TenantId, ct);
            if (identityTenant is null || !identityTenant.IsActive)
                return Result<AuthResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

            var identityToken = _accessTokenService.GenerateSessionToken(identityUser, membership.TenantId, membership.Role);
            var (identityRefresh, identityRefreshExpiry) = await _refreshTokenService.CreateAsync(
                identityUser.Id, membership.TenantId, RefreshUserType.Identity, ct);

            var identityModules = await _sessionModules.GetEnabledModuleKeysAsync(
                membership.TenantId, identityTenant, ct);

            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                identityUser.Id,
                identityUser.FullName,
                identityUser.Email.Value,
                membership.Role,
                membership.TenantId,
                identityToken,
                identityTenant.PlanCode,
                identityModules)
            {
                RefreshToken       = identityRefresh,
                RefreshTokenExpiry = identityRefreshExpiry,
            });
        }

        // Non-superadmin legacy (tabla users por tenant).
        var tenants = await _tenantRepository.GetAllAsync(ct);
        var matches = new List<Domain.Auth.Entities.User>();

        foreach (var tenant in tenants.Where(t => t.IsActive))
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

        var passwordValid = _passwordHasher.VerifyPassword(command.Password, single.PasswordHash);
        if (!passwordValid)
            return Result<AuthResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        var token       = _jwtService.GenerateToken(single);
        var tenantEntity = await _tenantRepository.GetByIdAsync(single.TenantId, ct);
        var (legacyRefresh, legacyRefreshExpiry) = await _refreshTokenService.CreateAsync(
            single.Id, single.TenantId, RefreshUserType.Legacy, ct);

        var legacyModules = tenantEntity is null
            ? Array.Empty<string>()
            : await _sessionModules.GetEnabledModuleKeysAsync(single.TenantId, tenantEntity, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            single.Id,
            single.FullName,
            single.Email.Value,
            single.Role,
            single.TenantId,
            token,
            tenantEntity?.PlanCode,
            legacyModules)
        {
            RefreshToken       = legacyRefresh,
            RefreshTokenExpiry = legacyRefreshExpiry,
        });
    }
}
