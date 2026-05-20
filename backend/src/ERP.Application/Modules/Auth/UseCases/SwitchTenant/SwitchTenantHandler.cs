using MediatR;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.SwitchTenant;

public class SwitchTenantHandler : IRequestHandler<SwitchTenantCommand, Result<AuthResponseDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IJwtService _jwtService;
    private readonly ISessionModulesResolver _sessionModules;

    public SwitchTenantHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IJwtService jwtService,
        ISessionModulesResolver sessionModules)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _jwtService = jwtService;
        _sessionModules = sessionModules;
    }

    public async Task<Result<AuthResponseDto>> Handle(SwitchTenantCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<AuthResponseDto>.Failure("No autenticado.");

        var user = await _userRepository.GetByIdSystemAsync(_currentUser.UserId, ct);
        if (user is null)
            return Result<AuthResponseDto>.Failure("Usuario no encontrado.");

        if (!string.Equals(user.Role, "SuperAdmin", StringComparison.Ordinal))
            return Result<AuthResponseDto>.Failure("No autorizado.");

        // Permite "volver al panel global" para SuperAdmin: tenant_id = Guid.Empty.
        // Esto evita una pantalla intermedia cuando el SuperAdmin ya está impersonando una empresa.
        if (command.TenantId == Guid.Empty)
        {
            var globalToken = _jwtService.GenerateToken(user, Guid.Empty);
            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                user.Id,
                user.FullName,
                user.Email.Value,
                user.Role,
                Guid.Empty,
                globalToken,
                PlanCode: null,
                EnabledModules: TenantSubscriptionCatalog.AllModuleKeys));
        }

        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Empresa no encontrada o inactiva.");

        var token = _jwtService.GenerateToken(user, tenant.Id);

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, tenant, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Role,
            tenant.Id,
            token,
            tenant.PlanCode,
            modules));
    }
}

