using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using MediatR;

namespace ERP.Application.Auth.UseCases.GlobalLogin;

public sealed class GlobalLoginHandler
    : IRequestHandler<GlobalLoginCommand, Result<AuthResponseDto>>
{
    private static readonly Guid GlobalTenantId = Guid.Empty;

    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;

    public GlobalLoginHandler(
        IAccessRepository accessRepository,
        IAccessTokenService accessTokenService,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService
    )
    {
        _accessRepository = accessRepository;
        _accessTokenService = accessTokenService;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        GlobalLoginCommand command,
        CancellationToken cancellationToken
    )
    {
        var username = command.Username.Trim();

        var identityUser = await _accessRepository.GetUserByUsernameAsync(
            username,
            cancellationToken
        );

        if (identityUser is null)
            return Result<AuthResponseDto>.Failure("Credenciales inválidas.");

        if (!identityUser.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        if (!_passwordHasher.VerifyPassword(command.Password, identityUser.PasswordHash))
            return Result<AuthResponseDto>.Failure("Credenciales inválidas.");

        var globalRole = await _accessRepository.GetActiveGlobalUserRoleAsync(
            identityUser.Id,
            SecurityRoles.Admin,
            cancellationToken
        );

        if (globalRole is null)
            return Result<AuthResponseDto>.Failure("No autorizado como administrador global.");

        if (identityUser.RequirePasswordReset)
            return Result<AuthResponseDto>.Failure(
                "Debe completar el cambio de contraseña antes de acceder como administrador global."
            );

        var token = _accessTokenService.GenerateSessionToken(
            identityUser,
            GlobalTenantId,
            SecurityRoles.Admin
        );

        var (refreshToken, refreshTokenExpiry) = await _refreshTokenService.CreateAsync(
            identityUser.Id,
            GlobalTenantId,
            null,
            RefreshUserType.Identity,
            cancellationToken
        );

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                identityUser.Id,
                identityUser.FullName,
                identityUser.Username,
                identityUser.Email?.Value,
                SecurityRoles.Admin,
                GlobalTenantId,
                token
            )
            {
                CompanyId = null,
                RequiresCompanySelection = false,
                OnboardingCompleted = true,
                RefreshToken = refreshToken,
                RefreshTokenExpiry = refreshTokenExpiry,
            }
        );
    }
}
