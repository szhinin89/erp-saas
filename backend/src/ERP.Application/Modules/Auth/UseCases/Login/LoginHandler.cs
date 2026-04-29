using ERP.Application.Common;
using ERP.Application.Auth.DTOs;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Application.Auth.UseCases.Login;

public class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public LoginHandler(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService     = jwtService;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(
        LoginCommand command,
        CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(command.Email, command.TenantId, ct);
        if (user is null)
            return Result<AuthResponseDto>.Failure("Credenciales invalidas.");

        if (!user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        var passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!passwordValid)
            return Result<AuthResponseDto>.Failure("Credenciales invalidas.");

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
