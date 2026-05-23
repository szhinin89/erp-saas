using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using MediatR;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

/// <summary>
/// Devuelve permisos efectivos del usuario autenticado para UI (runtime read).
/// Usuarios regulares: <see cref="IEffectivePermissionKeysProvider"/> (auth path).
/// Admin/SuperAdmin: wildcard (UX; la API sigue filtrando por plan vía <see cref="Authorization.IRuntimePermissionAuthorizer"/>).
/// </summary>
public class GetMyPermissionsHandler : IRequestHandler<GetMyPermissionsQuery, Result<MyPermissionsDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly IEffectivePermissionKeysProvider _permissionKeys;
    private readonly ICompanyContextProvider _companyContext;

    public GetMyPermissionsHandler(
        ICurrentUser currentUser,
        ICurrentSubscriber currentSubscriber,
        ISubscriberRepository subscriberRepository,
        ISessionModulesResolver sessionModules,
        IEffectivePermissionKeysProvider permissionKeys,
        ICompanyContextProvider companyContext)
    {
        _currentUser = currentUser;
        _currentSubscriber = currentSubscriber;
        _subscriberRepository = subscriberRepository;
        _sessionModules = sessionModules;
        _permissionKeys = permissionKeys;
        _companyContext = companyContext;
    }

    public Task<Result<MyPermissionsDto>> HandleAsync(CancellationToken ct = default)
        => Handle(new GetMyPermissionsQuery(), ct);

    public async Task<Result<MyPermissionsDto>> Handle(GetMyPermissionsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || !_currentSubscriber.IsAuthenticated)
            return Result<MyPermissionsDto>.Failure("No autenticado.");

        var tenant = await _subscriberRepository.GetByIdAsync(_currentSubscriber.SubscriberId, ct);
        var plan = tenant?.PlanCode;
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(_currentSubscriber.SubscriberId, ct);

        var role = _currentUser.Role ?? string.Empty;
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(new[] { "*" }, plan, modules));

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(new[] { "*" }, plan, modules));

        var context = await _companyContext.ResolveOperationalForCurrentUserAsync(ct);
        if (context is null || context.CompanyId == Guid.Empty)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>(), plan, modules));

        if (!context.IsActiveMembership || context.ProfileId is null)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>(), plan, modules));

        var allowed = await _permissionKeys.GetAllowedKeysAsync(
            _currentSubscriber.SubscriberId,
            context.CompanyId,
            _currentUser.UserId,
            context.ProfileId.Value,
            ct);

        return Result<MyPermissionsDto>.Success(new MyPermissionsDto(allowed, plan, modules));
    }
}
