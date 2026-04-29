using ERP.Application.Common;
using ERP.Application.Auth.DTOs;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.Register;

public class RegisterHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IJwtService _jwtService;

    public RegisterHandler(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IJwtService jwtService)
    {
        _userRepository   = userRepository;
        _tenantRepository = tenantRepository;
        _jwtService       = jwtService;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(
        RegisterCommand command,
        CancellationToken ct = default)
    {
        var tenantExists = await _tenantRepository.ExistsAsync(command.TenantId, ct);
        if (!tenantExists)
            return Result<AuthResponseDto>.Failure("El tenant no existe.");

        var emailExists = await _userRepository.ExistsAsync(command.Email, command.TenantId, ct);
        if (emailExists)
            return Result<AuthResponseDto>.Failure("Ya existe un usuario con ese email.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);

        var user = User.Create(
            command.TenantId,
            command.FirstName,
            command.LastName,
            command.Email,
            passwordHash,
            command.Role,
            Guid.Empty);

        await _userRepository.AddAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        var token = _jwtService.GenerateToken(user);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Role,
            user.TenantId,
            token));
    }
}
