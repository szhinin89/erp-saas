using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Platform.Auth.UseCases.PlatformLogin;

/// <summary>Login canónico para operadores platform (operador platform global).</summary>
public sealed class PlatformLoginHandler : IRequestHandler<PlatformLoginCommand, Result<AuthResponseDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;

    public PlatformLoginHandler(
        IAccessRepository accessRepository,
        IAccessTokenService accessTokenService,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService)
    {
        _accessRepository = accessRepository;
        _accessTokenService = accessTokenService;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<AuthResponseDto>> Handle(PlatformLoginCommand command, CancellationToken ct)
    {
        if (!_deployment.IsPlatformPanelEnabled)
            return Result<AuthResponseDto>.Failure(DeploymentAuthMessages.PlatformPanelDisabled);

        var email = command.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return Result<AuthResponseDto>.Failure("Email requerido.");

        var user = await _accessRepository.GetPlatformUserByEmailAsync(email, ct);
        if (user is null)
            return Result<AuthResponseDto>.Failure("Credenciales inválidas.");

        if (!user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        if (user.RequirePasswordReset)
            return Result<AuthResponseDto>.Failure("Debe restablecer su contraseña antes de iniciar sesión.");

        if (!_passwordHasher.VerifyPassword(command.Password, user.PasswordHash))
            return Result<AuthResponseDto>.Failure("Credenciales inválidas.");

        var jwtRole = user.PlatformRole switch
        {
            Domain.Access.Enums.PlatformRole.PlatformOperator => PlatformAuthConstants.JwtPlatformOperatorRole,
            Domain.Access.Enums.PlatformRole.Support => "Support",
            Domain.Access.Enums.PlatformRole.BillingAdmin => "BillingAdmin",
            Domain.Access.Enums.PlatformRole.Auditor => "Auditor",
            _ => PlatformAuthConstants.JwtPlatformOperatorRole,
        };

        var token = _accessTokenService.GeneratePlatformSessionToken(user);
        var (refresh, refreshExpiry) = await _refreshTokenService.CreateAsync(
            user.Id, Guid.Empty, null, RefreshUserType.Platform, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            jwtRole,
            Guid.Empty,
            token,
            PlanCode: null,
            EnabledModules: SubscriberSubscriptionCatalog.AllModuleKeys)
        {
            RefreshToken = refresh,
            RefreshTokenExpiry = refreshExpiry,
            UserType = user.UserType.ToString(),
            PlatformRole = user.PlatformRole?.ToString(),
        });
    }
}
