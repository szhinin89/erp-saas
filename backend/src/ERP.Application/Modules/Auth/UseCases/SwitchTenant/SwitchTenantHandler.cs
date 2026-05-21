using MediatR;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Auth.UseCases.SwitchSubscriber;

public class SwitchSubscriberHandler : IRequestHandler<SwitchSubscriberCommand, Result<AuthResponseDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IAccessRepository _accessRepository;
    private readonly ISubscriberRepository _tenantRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly ISessionModulesResolver _sessionModules;

    public SwitchSubscriberHandler(
        ICurrentUser currentUser,
        IAccessRepository accessRepository,
        ISubscriberRepository tenantRepository,
        IAccessTokenService accessTokenService,
        ISessionModulesResolver sessionModules)
    {
        _currentUser = currentUser;
        _accessRepository = accessRepository;
        _tenantRepository = tenantRepository;
        _accessTokenService = accessTokenService;
        _sessionModules = sessionModules;
    }

    public async Task<Result<AuthResponseDto>> Handle(SwitchSubscriberCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<AuthResponseDto>.Failure("No autenticado.");

        var user = await _accessRepository.GetUserByIdAsync(_currentUser.UserId, ct);
        if (user is null || !user.IsPlatformSuperAdmin)
            return Result<AuthResponseDto>.Failure("No autorizado.");

        if (command.SubscriberId == Guid.Empty)
        {
            var globalToken = _accessTokenService.GeneratePlatformSessionToken(user);
            return Result<AuthResponseDto>.Success(new AuthResponseDto(
                user.Id,
                user.FullName,
                user.Email.Value,
                "SuperAdmin",
                Guid.Empty,
                globalToken,
                PlanCode: null,
                EnabledModules: SubscriberSubscriptionCatalog.AllModuleKeys));
        }

        var tenant = await _tenantRepository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Empresa no encontrada o inactiva.");

        var token = _accessTokenService.GenerateSessionToken(
            user.Id,
            user.Email.Value,
            user.FullName,
            tenant.Id,
            "SuperAdmin",
            companyId: default,
            userType: user.UserType,
            platformRole: user.PlatformRole);

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            "SuperAdmin",
            tenant.Id,
            token,
            tenant.PlanCode,
            modules));
    }
}
