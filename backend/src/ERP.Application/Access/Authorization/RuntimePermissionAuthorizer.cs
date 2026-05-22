using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.Authorization;

public sealed class RuntimePermissionAuthorizer : IRuntimePermissionAuthorizer
{
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ISubscriberRepository _tenantRepository;
    private readonly ISubscriberEntitlementsService _entitlements;
    private readonly ICompanyContextProvider _companyContext;
    private readonly IEffectivePermissionKeysProvider _permissionKeys;

    public RuntimePermissionAuthorizer(
        ICurrentSubscriber currentSubscriber,
        ISubscriberRepository tenantRepository,
        ISubscriberEntitlementsService entitlements,
        ICompanyContextProvider companyContext,
        IEffectivePermissionKeysProvider permissionKeys)
    {
        _currentSubscriber = currentSubscriber;
        _tenantRepository = tenantRepository;
        _entitlements = entitlements;
        _companyContext = companyContext;
        _permissionKeys = permissionKeys;
    }

    public async Task<bool> IsAuthorizedAsync(
        string permissionKey,
        Guid userId,
        string role,
        CancellationToken ct = default)
    {
        if (!_currentSubscriber.IsAuthenticated)
            return false;

        var subscriberId = _currentSubscriber.SubscriberId;

        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && subscriberId == Guid.Empty)
            return true;

        if (await _tenantRepository.GetByIdAsync(subscriberId, ct) is null)
            return false;

        var planAllows = await _entitlements.AllowsPermissionAsync(subscriberId, permissionKey, ct);

        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return planAllows;

        if (!planAllows)
            return false;

        if (userId == Guid.Empty)
            return false;

        var context = await _companyContext.ResolveOperationalForUserAsync(userId, ct);
        if (context is null
            || context.CompanyId == Guid.Empty
            || !context.IsActiveMembership
            || context.ProfileId is null)
            return false;

        var allowed = await _permissionKeys.GetAllowedKeysAsync(
            subscriberId,
            context.CompanyId,
            userId,
            context.ProfileId.Value,
            ct);

        return allowed.Any(k => string.Equals(k, permissionKey, StringComparison.OrdinalIgnoreCase));
    }
}
