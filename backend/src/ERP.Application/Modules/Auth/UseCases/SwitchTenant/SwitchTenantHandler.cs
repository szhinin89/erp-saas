using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.SwitchTenant;

public class SwitchTenantHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IJwtService _jwtService;

    public SwitchTenantHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IJwtService jwtService)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(SwitchTenantCommand command, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<AuthResponseDto>.Failure("No autenticado.");

        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Empresa no encontrada o inactiva.");

        var user = await _userRepository.GetByIdSystemAsync(_currentUser.UserId, ct);
        if (user is null)
            return Result<AuthResponseDto>.Failure("Usuario no encontrado.");

        if (!string.Equals(user.Role, "SuperAdmin", StringComparison.Ordinal))
            return Result<AuthResponseDto>.Failure("No autorizado.");

        var token = _jwtService.GenerateToken(user, tenant.Id);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Role,
            tenant.Id,
            token));
    }
}

