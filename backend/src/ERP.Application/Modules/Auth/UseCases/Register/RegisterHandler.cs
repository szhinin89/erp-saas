using MediatR;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.Register;

public class RegisterHandler : IRequestHandler<RegisterCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IJwtService _jwtService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionModulesResolver _sessionModules;

    public RegisterHandler(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IJwtService jwtService,
        IPasswordHasher passwordHasher,
        ISessionModulesResolver sessionModules)
    {
        _userRepository   = userRepository;
        _tenantRepository = tenantRepository;
        _jwtService       = jwtService;
        _passwordHasher   = passwordHasher;
        _sessionModules   = sessionModules;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        RegisterCommand command,
        CancellationToken ct)
    {
        if (string.Equals(command.Role, "SuperAdmin", StringComparison.Ordinal))
        {
            var existsSuperAdmin = await _userRepository.AnySuperAdminAsync(ct);
            if (existsSuperAdmin)
                return Result<AuthResponseDto>.Failure("Ya existe un SuperAdmin en el sistema.");
        }

        var tenantExists = await _tenantRepository.ExistsAsync(command.TenantId, ct);
        if (!tenantExists)
            return Result<AuthResponseDto>.Failure("El tenant no existe.");

        var emailExists = await _userRepository.ExistsAsync(command.Email, command.TenantId, ct);
        if (emailExists)
            return Result<AuthResponseDto>.Failure("Ya existe un usuario con ese email.");

        var passwordHash = _passwordHasher.HashPassword(command.Password);

        // En auto-registro el usuario es su propio creador: se pre-genera el ID
        // para usarlo como createdBy antes de persistir.
        var newId = Guid.NewGuid();
        var user  = User.Create(
            command.TenantId,
            command.FirstName,
            command.LastName,
            command.Email,
            passwordHash,
            command.Role,
            createdBy: newId);

        await _userRepository.AddAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        var token = _jwtService.GenerateToken(user);

        var tenantEntity = await _tenantRepository.GetByIdAsync(user.TenantId, ct);

        var modules = tenantEntity is null
            ? Array.Empty<string>()
            : await _sessionModules.GetEnabledModuleKeysAsync(user.TenantId, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Role,
            user.TenantId,
            token,
            tenantEntity?.PlanCode,
            modules));
    }
}
