using System.Linq;
using ERP.Application.Subscriptions;

namespace ERP.Application.Common;

/// <summary>
/// Catálogo de claves de módulo y validación. La autoridad de módulos habilitados es
/// <see cref="ISubscriberEntitlementsService"/> vía <see cref="ResolveEnabledModulesAsync"/>.
/// </summary>
public static class SubscriberSubscriptionCatalog
{
    private static readonly IReadOnlyList<string> EmptyModules = Array.Empty<string>();

    /// <summary>Claves conocidas para validación de entrada operador platform (legacy español).</summary>
    public static readonly IReadOnlyList<string> AllModuleKeys = new[]
    {
        "access",
        "accounting",
        "compras",
        "gastos",
        "inventario",
        "logistica",
        "rrhh",
        "saas",
        "ventas",
    };

    /// <summary>Catálogo comercial canónico (inglés, alineado a <c>ResourceRef</c> / menú).</summary>
    public static readonly IReadOnlyList<string> CanonicalModuleKeys = new[]
    {
        "access",
        "accounting",
        "expenses",
        "inventory",
        "logistics",
        "payroll",
        "purchases",
        "sales",
        "saas",
    };

    /// <summary>
    /// Indica si el tenant no tiene el catálogo completo (derivado de módulos efectivos, no de JSON legacy).
    /// Lista vacía = restringido (fail-closed), no “todos los módulos”.
    /// </summary>
    public static bool HasModuleRestrictionsFromModules(IReadOnlyCollection<string> enabledModules)
    {
        if (enabledModules is null || enabledModules.Count == 0)
            return true;

        var enabled = new HashSet<string>(
            enabledModules.Select(NormalizeStoredModuleKey),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in CanonicalModuleKeys)
        {
            if (!enabled.Contains(key))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resuelve módulos habilitados desde el modelo de suscripción (fuente única).
    /// Sin suscripción activa → vacío (fail-closed).
    /// </summary>
    public static async Task<IReadOnlyList<string>> ResolveEnabledModulesAsync(
        Guid subscriberId,
        ISubscriberEntitlementsService entitlements,
        CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return EmptyModules;

        var modules = await entitlements.GetEnabledModuleKeysAsync(subscriberId, ct);
        return modules is IReadOnlyList<string> list
            ? list
            : modules.ToList();
    }

    /// <summary>
    /// Prefijos de permiso (inglés API + español legacy) → clave canónica de módulo (<c>ResourceRef</c> / menú).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> PermissionPrefixToCanonicalModule =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sales"] = "sales",
            ["inventory"] = "inventory",
            ["purchases"] = "purchases",
            ["logistics"] = "logistics",
            ["finance"] = "accounting",
            ["admin"] = "access",
            ["ventas"] = "sales",
            ["inventario"] = "inventory",
            ["compras"] = "purchases",
            ["gastos"] = "expenses",
            ["expenses"] = "expenses",
            ["logistica"] = "logistics",
            ["rrhh"] = "payroll",
            ["accounting"] = "accounting",
            ["access"] = "access",
            ["settings"] = "access",
            ["masterdata"] = "sales",
            ["saas"] = "saas",
        };

    public static bool TryGetModuleKeyForPermission(string permissionKey, out string moduleKey)
    {
        moduleKey = string.Empty;
        var dot = permissionKey.IndexOf('.');
        if (dot <= 0)
            return false;

        var prefix = permissionKey[..dot].Trim().ToLowerInvariant();
        if (!PermissionPrefixToCanonicalModule.TryGetValue(prefix, out var canonical))
            return false;

        moduleKey = canonical;
        return true;
    }

    private static readonly HashSet<string> SubscriberAccountPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "saas.companies.view",
        "saas.companies.create",
        "saas.companies.update",
        "saas.billing.view",
    };

    /// <summary>
    /// Permisos de cuenta SaaS (empresas operativas, billing) no dependen del módulo comercial <c>saas</c>.
    /// Todo suscriptor autenticado con rol Admin debe poder gestionar su cuenta.
    /// </summary>
    public static bool IsSubscriberAccountPermission(string permissionKey) =>
        SubscriberAccountPermissions.Contains(permissionKey.Trim());

    /// <summary>
    /// Gating por plan vía entitlements (SoT). Prefijos no comerciales → permitido.
    /// Permisos de cuenta suscriptor → siempre permitidos.
    /// </summary>
    public static async Task<bool> TenantAllowsPermissionAsync(
        Guid subscriberId,
        ISubscriberEntitlementsService entitlements,
        string permissionKey,
        CancellationToken ct = default)
    {
        if (IsSubscriberAccountPermission(permissionKey))
            return true;

        if (!TryGetModuleKeyForPermission(permissionKey, out var module))
            return true;

        if (subscriberId == Guid.Empty)
            return true;

        var enabled = await entitlements.GetEnabledModuleKeysAsync(subscriberId, ct);
        return IsModuleEnabled(enabled, module);
    }

    private static bool IsModuleEnabled(IReadOnlyCollection<string> enabledModules, string canonicalModuleKey)
    {
        if (enabledModules.Count == 0)
            return false;

        foreach (var key in enabledModules)
        {
            if (string.Equals(NormalizeStoredModuleKey(key), canonicalModuleKey, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Normaliza clave de entrada (español legacy o inglés) a módulo canónico.</summary>
    public static string NormalizeModuleKey(string key) => NormalizeStoredModuleKey(key);

    /// <summary>Lista normalizada, sin duplicados, ordenada.</summary>
    public static IReadOnlyList<string> NormalizeModuleKeysInput(IReadOnlyList<string> keys)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in keys)
        {
            var n = NormalizeModuleKey(k);
            if (n.Length > 0)
                set.Add(n);
        }

        return set.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static string NormalizeStoredModuleKey(string key)
    {
        var trimmed = (key ?? string.Empty).Trim().ToLowerInvariant();
        return PermissionPrefixToCanonicalModule.TryGetValue(trimmed, out var canonical)
            ? canonical
            : trimmed;
    }

    public static bool IsKnownModuleKey(string key)
    {
        var n = NormalizeModuleKey(key);
        if (n.Length == 0)
            return false;
        return CanonicalModuleKeys.Contains(n, StringComparer.OrdinalIgnoreCase);
    }

    public static void ValidateModuleKeysOrThrow(IReadOnlyList<string> keys)
    {
        foreach (var k in keys)
        {
            var n = NormalizeModuleKey(k);
            if (n.Length == 0)
                throw new ArgumentException("Módulo vacío no permitido.", nameof(keys));
            if (!CanonicalModuleKeys.Contains(n, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Módulo desconocido: '{k}'. Válidos: {string.Join(", ", CanonicalModuleKeys)}.",
                    nameof(keys));
        }
    }
}
