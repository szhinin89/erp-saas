using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using MediatR;

namespace ERP.Application.Auth.UseCases.ReturnToGlobal;

/// <summary>
/// Ver <see cref="ReturnToGlobalCommand"/>. El token que invoca este handler es un token operativo
/// (tenant_id real, no Guid.Empty), así que la autorización real no viene de la policy del
/// controller (que solo exige sesión autenticada) sino del chequeo interno de
/// <see cref="ICurrentOperatorContext.IsOperatorMode"/> en el paso 1 — documentado también en
/// <c>GlobalAuthController</c>.
/// </summary>
public sealed class ReturnToGlobalHandler : IRequestHandler<ReturnToGlobalCommand, Result<AuthResponseDto>>
{
    private static readonly Guid GlobalTenantId = Guid.Empty;

    private readonly ICurrentOperatorContext _currentOperatorContext;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;

    public ReturnToGlobalHandler(
        ICurrentOperatorContext currentOperatorContext,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        IRefreshTokenService refreshTokenService
    )
    {
        _currentOperatorContext = currentOperatorContext;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        ReturnToGlobalCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!_currentOperatorContext.IsOperatorMode || _currentOperatorContext.GlobalAdminUserId is null)
            return Result<AuthResponseDto>.Failure(
                "Esta sesión no proviene de un operador global."
            );

        var globalAdminUserId = _currentOperatorContext.GlobalAdminUserId.Value;

        var globalRole = await _accessRepository.GetActiveGlobalUserRoleAsync(
            globalAdminUserId,
            SecurityRoles.Admin,
            cancellationToken
        );
        if (globalRole is null)
            return Result<AuthResponseDto>.Failure("Rol de administrador global ya no está activo.");

        var user = await _accessRepository.GetUserByIdAsync(globalAdminUserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario no válido.");

        var token = _tokenService.GenerateSessionToken(user, GlobalTenantId, SecurityRoles.Admin);

        var (refreshToken, refreshTokenExpiry) = await _refreshTokenService.CreateAsync(
            user.Id,
            GlobalTenantId,
            null,
            RefreshUserType.Identity,
            cancellationToken
        );

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                user.Id,
                user.FullName,
                user.Username,
                user.Email?.Value,
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
                OperatorMode = false,
            }
        );
    }
}
