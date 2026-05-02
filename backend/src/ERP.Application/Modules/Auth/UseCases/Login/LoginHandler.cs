using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.Login;

public class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IJwtService _jwtService;

    public LoginHandler(IUserRepository userRepository, ITenantRepository tenantRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _jwtService     = jwtService;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(
        LoginCommand command,
        CancellationToken ct = default)
    {
        // SuperAdmin auto-detection: allow global login (no tenant context).
        var superAdmin = await _userRepository.GetSingleSuperAdminByEmailAsync(command.Email, ct);
        if (superAdmin is not null)
        {
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

        // Non-superadmin: resolve tenant membership by email.
        var tenants = await _tenantRepository.GetAllAsync(ct);
        var matches = new List<Domain.Auth.Entities.User>();

        foreach (var tenant in tenants.Where(t => t.IsActive))
        {
            var user = await _userRepository.GetByEmailSystemAsync(command.Email, tenant.Id, ct);
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
