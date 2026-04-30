using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.SuperAdminLogin;

public class SuperAdminLoginHandler
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public SuperAdminLoginHandler(ITenantRepository tenantRepository, IUserRepository userRepository, IJwtService jwtService)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(SuperAdminLoginCommand command, CancellationToken ct = default)
    {
        var email = command.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return Result<AuthResponseDto>.Failure("Email requerido.");

        // Global login: find SuperAdmin user across all tenants.
        var tenants = await _tenantRepository.GetAllAsync(ct);
        if (tenants.Count == 0)
            return Result<AuthResponseDto>.Failure("No existen empresas configuradas.");

        Domain.Auth.Entities.User? user = null;
        foreach (var tenant in tenants.Where(t => t.IsActive))
        {
            user = await _userRepository.GetByEmailSystemAsync(email, tenant.Id, ct);
            if (user is not null) break;
        }

        if (user is null)
            return Result<AuthResponseDto>.Failure("Credenciales invalidas.");

        if (!user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        if (!string.Equals(user.Role, "SuperAdmin", StringComparison.Ordinal))
            return Result<AuthResponseDto>.Failure("No autorizado.");

        var passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!passwordValid)
            return Result<AuthResponseDto>.Failure("Credenciales invalidas.");

        // Global token: tenant_id = Guid.Empty, must select a tenant next.
        var token = _jwtService.GenerateToken(user, Guid.Empty);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Role,
            Guid.Empty,
            token));
    }
}

