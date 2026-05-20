using System.Linq;
using System.Text.Json;
using ERP.Application.Subscriptions;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Common;

/// <summary>
/// Catálogo legacy de claves de módulo y validación. La autoridad de módulos habilitados es
/// <see cref="ITenantEntitlementsService"/> vía <see cref="ResolveEnabledModulesAsync"/>.
/// </summary>
public static class TenantSubscriptionCatalog
{
    private static readonly IReadOnlyList<string> EmptyModules = Array.Empty<string>();

    /// <summary>Claves conocidas para validación de entrada SuperAdmin (legacy español).</summary>
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

    /// <summary>
    /// Resuelve módulos habilitados desde el modelo de suscripción (fuente única).
    /// Sin suscripción activa → vacío (fail-closed).
    /// </summary>
    public static async Task<IReadOnlyList<string>> ResolveEnabledModulesAsync(
        Guid tenantId,
        ITenantEntitlementsService entitlements,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return EmptyModules;

        var modules = await entitlements.GetEnabledModuleKeysAsync(tenantId, ct);
        return modules is IReadOnlyList<string> list
            ? list
            : modules.ToList();
    }

    /// <summary>
    /// Lectura de caché legacy (<c>EnabledModulesJson</c>) solo para compatibilidad de visualización.
    /// No usar como autoridad: null/vacío/inválido → vacío (nunca <see cref="AllModuleKeys"/>).
    /// </summary>
    public static IReadOnlyList<string> GetEffectiveEnabledModules(Tenant tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.EnabledModulesJson))
            return EmptyModules;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(tenant.EnabledModulesJson);
            if (parsed is null || parsed.Count == 0)
                return EmptyModules;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in parsed)
            {
                var t = (s ?? string.Empty).Trim().ToLowerInvariant();
                if (t.Length > 0 && AllModuleKeys.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                    set.Add(t);
            }

            if (set.Count == 0)
                return EmptyModules;

            return set.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }
        catch (JsonException)
        {
            return EmptyModules;
        }
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

    /// <summary>
    /// Gating por plan vía entitlements (SoT). Prefijos no comerciales → permitido.
    /// </summary>
    public static async Task<bool> TenantAllowsPermissionAsync(
        Guid tenantId,
        ITenantEntitlementsService entitlements,
        string permissionKey,
        CancellationToken ct = default)
    {
        if (!TryGetModuleKeyForPermission(permissionKey, out var module))
            return true;

        if (tenantId == Guid.Empty)
            return true;

        var enabled = await entitlements.GetEnabledModuleKeysAsync(tenantId, ct);
        return IsModuleEnabled(enabled, module);
    }

    /// <summary>
    /// Lectura legacy JSON; usa normalización canónica. Preferir <see cref="TenantAllowsPermissionAsync"/>.
    /// </summary>
    public static bool TenantAllowsPermission(Tenant tenant, string permissionKey)
    {
        if (!TryGetModuleKeyForPermission(permissionKey, out var module))
            return true;

        var enabled = GetEffectiveEnabledModules(tenant);
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

    private static string NormalizeStoredModuleKey(string key)
    {
        var trimmed = (key ?? string.Empty).Trim().ToLowerInvariant();
        return PermissionPrefixToCanonicalModule.TryGetValue(trimmed, out var canonical)
            ? canonical
            : trimmed;
    }

    public static void ValidateModuleKeysOrThrow(IReadOnlyList<string> keys)
    {
        foreach (var k in keys)
        {
            var n = (k ?? string.Empty).Trim().ToLowerInvariant();
            if (n.Length == 0)
                throw new ArgumentException("Módulo vacío no permitido.", nameof(keys));
            if (!AllModuleKeys.Contains(n, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Módulo desconocido: '{k}'. Válidos: {string.Join(", ", AllModuleKeys)}.", nameof(keys));
        }
    }
}
