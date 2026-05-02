using System.Linq;
using System.Text.Json;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Common;

/// <summary>
/// Claves de módulo contratables por tenant. Deben coincidir con el primer segmento de <c>permissionKey</c> (p. ej. <c>catalog.brands.view</c> → <c>catalog</c>).
/// </summary>
public static class TenantSubscriptionCatalog
{
    public static readonly IReadOnlyList<string> AllModuleKeys = new[]
    {
        "catalog",
        "accounting",
        "saas",
        "access",
    };

    /// <summary>JSON <c>null</c> o vacío en el tenant = todos los módulos (compatibilidad).</summary>
    public static IReadOnlyList<string> GetEffectiveEnabledModules(Tenant tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.EnabledModulesJson))
            return AllModuleKeys;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(tenant.EnabledModulesJson);
            if (parsed is null || parsed.Count == 0)
                return AllModuleKeys;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in parsed)
            {
                var t = (s ?? string.Empty).Trim().ToLowerInvariant();
                if (t.Length > 0 && AllModuleKeys.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                    set.Add(t);
            }

            return set.Count == 0 ? AllModuleKeys : set.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }
        catch (JsonException)
        {
            return AllModuleKeys;
        }
    }

    public static bool TryGetModuleKeyForPermission(string permissionKey, out string moduleKey)
    {
        moduleKey = string.Empty;
        var dot = permissionKey.IndexOf('.');
        if (dot <= 0)
            return false;

        var prefix = permissionKey[..dot].Trim().ToLowerInvariant();
        if (!AllModuleKeys.Any(x => string.Equals(x, prefix, StringComparison.OrdinalIgnoreCase)))
            return false;

        moduleKey = prefix;
        return true;
    }

    /// <summary>Si el permiso no pertenece a un módulo conocido, no se restringe por suscripción.</summary>
    public static bool TenantAllowsPermission(Tenant tenant, string permissionKey)
    {
        if (!TryGetModuleKeyForPermission(permissionKey, out var module))
            return true;

        var enabled = GetEffectiveEnabledModules(tenant);
        return enabled.Contains(module, StringComparer.OrdinalIgnoreCase);
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
