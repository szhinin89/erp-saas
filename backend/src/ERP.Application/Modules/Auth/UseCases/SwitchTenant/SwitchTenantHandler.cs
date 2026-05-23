using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.SwitchSubscriber;

/// <summary>
/// Flujo Platform: SuperAdmin global selecciona un subscriber para impersonar.
/// Solo accesible con token Platform (IsPrimaryPlatformOperator = true).
/// Retorna <c>AuthResponseDto</c> con refresh token completo.
/// No confundir con <see cref="ERP.Application.Access.UseCases.SwitchSubscriber.SwitchSubscriberHandler"/>
/// que sirve el flujo ERP (Company Admin, bootstrap → session, retorna SessionResponseDto).
/// </summary>
public class SwitchSubscriberHandler : IRequestHandler<SwitchSubscriberCommand, Result<AuthResponseDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IAccessRepository _accessRepository;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly IRefreshTokenService _refreshTokenService;

    public SwitchSubscriberHandler(
        ICurrentUser currentUser,
        IAccessRepository accessRepository,
        ISubscriberRepository subscriberRepository,
        IAccessTokenService accessTokenService,
        ISessionModulesResolver sessionModules,
        IRefreshTokenService refreshTokenService)
    {
        _currentUser = currentUser;
        _accessRepository = accessRepository;
        _subscriberRepository = subscriberRepository;
        _accessTokenService = accessTokenService;
        _sessionModules = sessionModules;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<AuthResponseDto>> Handle(SwitchSubscriberCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<AuthResponseDto>.Failure("No autenticado.");

        IdentityUser? user = null;
        if (_currentUser.UserId != Guid.Empty)
            user = await _accessRepository.GetUserByIdAsync(_currentUser.UserId, ct);
        if (user is null && !string.IsNullOrWhiteSpace(_currentUser.Email))
            user = await _accessRepository.GetPlatformOperatorByEmailAsync(_currentUser.Email, ct);

        if (user is null || !user.IsPrimaryPlatformOperator)
            return Result<AuthResponseDto>.Failure("No autorizado.");

        if (command.SubscriberId == Guid.Empty)
        {
            var globalToken = _accessTokenService.GeneratePlatformSessionToken(user);
            var (globalRefresh, globalRefreshExpiry) = await _refreshTokenService.CreateAsync(
                user.Id, Guid.Empty, null, RefreshUserType.Platform, ct);

            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                user.Id,
                user.FullName,
                user.Email.Value,
                PlatformAuthConstants.JwtPlatformOperatorRole,
                Guid.Empty,
                globalToken,
                PlanCode: null,
                EnabledModules: SubscriberSubscriptionCatalog.AllModuleKeys)
            {
                RefreshToken = globalRefresh,
                RefreshTokenExpiry = globalRefreshExpiry,
                UserType = user.UserType.ToString(),
                PlatformRole = user.PlatformRole?.ToString(),
            });
        }

        var tenant = await _subscriberRepository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Empresa no encontrada o inactiva.");

        var token = _accessTokenService.GenerateSessionToken(
            user.Id,
            user.Email.Value,
            user.FullName,
            tenant.Id,
            PlatformAuthConstants.JwtPlatformOperatorRole,
            companyId: default,
            userType: user.UserType,
            platformRole: user.PlatformRole);

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, ct);
        var (refresh, refreshExpiry) = await _refreshTokenService.CreateAsync(
            user.Id, tenant.Id, null, RefreshUserType.Platform, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            PlatformAuthConstants.JwtPlatformOperatorRole,
            tenant.Id,
            token,
            tenant.PlanCode,
            modules)
        {
            RefreshToken = refresh,
            RefreshTokenExpiry = refreshExpiry,
            UserType = user.UserType.ToString(),
            PlatformRole = user.PlatformRole?.ToString(),
        });
    }
}
