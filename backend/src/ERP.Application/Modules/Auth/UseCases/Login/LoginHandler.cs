using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.Login;

public class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IJwtService _jwtService;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _accessTokenService;

    public LoginHandler(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IJwtService jwtService,
        IDeploymentFeatureFlags deployment,
        IAccessRepository accessRepository,
        IAccessTokenService accessTokenService)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _jwtService = jwtService;
        _deployment = deployment;
        _accessRepository = accessRepository;
        _accessTokenService = accessTokenService;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(
        LoginCommand command,
        CancellationToken ct = default)
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

            var superPasswordValid = BCrypt.Net.BCrypt.Verify(command.Password, superAdmin.PasswordHash);
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
                EnabledModules: TenantSubscriptionCatalog.AllModuleKeys));
        }

        // Usuarios solo en identity_users + memberships (p. ej. Admin creado al dar de alta una empresa).
        // El escaneo legacy en users por tenant no los encuentra; sin este paso /api/auth/login fallaba tras bootstrap.
        var identityUser = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (identityUser is not null)
        {
            if (!identityUser.IsActive)
                return Result<AuthResponseDto>.Failure("Usuario inactivo.");

            var identityPasswordOk = BCrypt.Net.BCrypt.Verify(command.Password, identityUser.PasswordHash);
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

            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                identityUser.Id,
                identityUser.FullName,
                identityUser.Email.Value,
                membership.Role,
                membership.TenantId,
                identityToken,
                identityTenant.PlanCode,
                TenantSubscriptionCatalog.GetEffectiveEnabledModules(identityTenant)));
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

        var passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, single.PasswordHash);
        if (!passwordValid)
            return Result<AuthResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        var token = _jwtService.GenerateToken(single);

        var tenantEntity = await _tenantRepository.GetByIdAsync(single.TenantId, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            single.Id,
            single.FullName,
            single.Email.Value,
            single.Role,
            single.TenantId,
            token,
            tenantEntity?.PlanCode,
            tenantEntity is null
                ? TenantSubscriptionCatalog.AllModuleKeys
                : TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenantEntity)));
    }
}
