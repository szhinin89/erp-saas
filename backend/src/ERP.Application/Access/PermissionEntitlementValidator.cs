using ERP.Application.Common;
using ERP.Application.Subscriptions;

namespace ERP.Application.Access;

/// <summary>
/// Implements <see cref="IPermissionEntitlementValidator"/> using the canonical
/// <see cref="SubscriberSubscriptionCatalog"/> logic — the exact same gate used by
/// <see cref="EffectivePermissionKeysProvider"/>. No parallel rules.
/// </summary>
public sealed class PermissionEntitlementValidator : IPermissionEntitlementValidator
{
    private readonly ISubscriberEntitlementsService _entitlements;

    public PermissionEntitlementValidator(ISubscriberEntitlementsService entitlements)
    {
        _entitlements = entitlements;
    }

    public async Task<IReadOnlyList<PermissionValidationResult>> ValidateAsync(
        Guid subscriberId,
        IEnumerable<string> permissionKeys,
        CancellationToken ct = default)
    {
        var results = new List<PermissionValidationResult>();
        foreach (var key in permissionKeys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            results.Add(await ValidateOneAsync(subscriberId, key.Trim(), ct));
        }
        return results;
    }

    public async Task<PermissionValidationResult> ValidateOneAsync(
        Guid subscriberId,
        string permissionKey,
        CancellationToken ct = default)
    {
        var key = permissionKey.Trim();

        // Subscriber account permissions (saas.billing.*, etc.) are always allowed.
        if (SubscriberSubscriptionCatalog.IsSubscriberAccountPermission(key))
            return new PermissionValidationResult(key, PermissionValidationStatus.Allowed);

        // Platform operators bypass all plan gating.
        if (subscriberId == Guid.Empty)
            return new PermissionValidationResult(key, PermissionValidationStatus.Allowed);

        // Check if prefix maps to a known commercial module.
        if (!SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission(key, out var module))
        {
            // Unknown prefix — not gated by any commercial module.
            // Policy: allow but flag for awareness.
            return new PermissionValidationResult(
                key,
                PermissionValidationStatus.UnknownPrefix,
                $"El prefijo '{key.Split('.')[0]}' no está mapeado a ningún módulo comercial. " +
                $"El permiso se guardará pero no está gobernado por el plan.");
        }

        // Check whether the subscriber's plan includes this module.
        var allowed = await SubscriberSubscriptionCatalog.SubscriberAllowsPermissionAsync(
            subscriberId, _entitlements, key, ct);

        if (allowed)
            return new PermissionValidationResult(key, PermissionValidationStatus.Allowed);

        // Get the enabled modules for a detailed rejection reason.
        var enabledModules = await _entitlements.GetEnabledModuleKeysAsync(subscriberId, ct);
        var moduleList     = string.Join(", ", enabledModules.DefaultIfEmpty("ninguno"));

        return new PermissionValidationResult(
            key,
            PermissionValidationStatus.BlockedByPlan,
            $"El módulo '{module}' no está incluido en el plan comercial actual. " +
            $"Módulos habilitados: [{moduleList}].");
    }
}
