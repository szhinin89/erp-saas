using MediatR;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.RefreshToken;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUserRepository      _userRepository;
    private readonly IAccessRepository    _accessRepository;
    private readonly ITenantRepository    _tenantRepository;
    private readonly IJwtService          _jwtService;
    private readonly IAccessTokenService  _accessTokenService;

    public RefreshTokenHandler(
        IRefreshTokenService refreshTokenService,
        IUserRepository userRepository,
        IAccessRepository accessRepository,
        ITenantRepository tenantRepository,
        IJwtService jwtService,
        IAccessTokenService accessTokenService)
    {
        _refreshTokenService = refreshTokenService;
        _userRepository      = userRepository;
        _accessRepository    = accessRepository;
        _tenantRepository    = tenantRepository;
        _jwtService          = jwtService;
        _accessTokenService  = accessTokenService;
    }

    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var validation = await _refreshTokenService.ValidateAndRotateAsync(command.RawRefreshToken, ct);
        if (!validation.IsValid)
            return Result<AuthResponseDto>.Failure(validation.Error!);

        return validation.UserType switch
        {
            RefreshUserType.Legacy   => await HandleLegacyAsync(validation, ct),
            RefreshUserType.Identity => await HandleIdentityAsync(validation, ct),
            _ => Result<AuthResponseDto>.Failure("Tipo de usuario no soportado en refresh."),
        };
    }

    // Backward-compatible helper used by integration tests.
    public Task<Result<AuthResponseDto>> HandleAsync(string rawRefreshToken, CancellationToken ct = default)
        => Handle(new RefreshTokenCommand(rawRefreshToken), ct);

    private async Task<Result<AuthResponseDto>> HandleLegacyAsync(
        RefreshTokenValidationResult v, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdSystemAsync(v.UserId, ct);
        if (user is null || !user.IsActive)
        {
            await _refreshTokenService.RevokeAllForUserAsync(v.UserId, v.TenantId, "Usuario inactivo", ct);
            return Result<AuthResponseDto>.Failure("Usuario no encontrado o inactivo.");
        }

        var tenant      = await _tenantRepository.GetByIdAsync(v.TenantId, ct);
        var accessToken = _jwtService.GenerateToken(user);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id, user.FullName, user.Email.Value,
            user.Role, v.TenantId, accessToken,
            tenant?.PlanCode,
            tenant is null
                ? TenantSubscriptionCatalog.AllModuleKeys
                : TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant))
        {
            RefreshToken       = v.NewToken,
            RefreshTokenExpiry = v.NewExpiry,
        });
    }

    private async Task<Result<AuthResponseDto>> HandleIdentityAsync(
        RefreshTokenValidationResult v, CancellationToken ct)
    {
        var user = await _accessRepository.GetUserByIdAsync(v.UserId, ct);
        if (user is null || !user.IsActive)
        {
            await _refreshTokenService.RevokeAllForUserAsync(v.UserId, v.TenantId, "Usuario inactivo", ct);
            return Result<AuthResponseDto>.Failure("Usuario no encontrado o inactivo.");
        }

        var membership = await _accessRepository.GetMembershipAsync(v.TenantId, v.UserId, ct);
        if (membership is null)
            return Result<AuthResponseDto>.Failure("Membresía no encontrada.");

        var tenant      = await _tenantRepository.GetByIdAsync(v.TenantId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Empresa no encontrada o inactiva.");

        var accessToken = _accessTokenService.GenerateSessionToken(user, v.TenantId, membership.Role);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id, user.FullName, user.Email.Value,
            membership.Role, v.TenantId, accessToken,
            tenant.PlanCode,
            TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant))
        {
            RefreshToken       = v.NewToken,
            RefreshTokenExpiry = v.NewExpiry,
        });
    }
}
