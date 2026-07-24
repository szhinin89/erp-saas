using ERP.Domain.Kernel;
using FluentAssertions;
using Xunit;

namespace ERP.Domain.Tests.Kernel;

/// <summary>
/// Invariantes del Platform Kernel: <see cref="KernelRegistry"/> es la única fuente de
/// verdad de permisos/navegación/módulos. Estos tests garantizan que la derivación por
/// reflexión produce un catálogo consistente (sin duplicados, sin huérfanos, sin
/// fragmentos legacy).
/// </summary>
public sealed class KernelRegistryTests
{
    // "sales", "purchases", "finance" y "cash" son vocabulario de negocio vigente y sancionado
    // en ERP_CORE_FREEZE.md (Nivel 1) — Sales/Accounting como módulos ERP, Purchases y Finance
    // (Condiciones de Crédito) ya implementados y activos, y CajaModule usa rutas reales
    // "/cash", "/cash/registers". No son fragmentos legacy: solo se bloquean aquí los nombres
    // realmente retirados y sin ningún uso actual en el Kernel ("purchasing" → sucedido por
    // "purchases", "expenses" sin uso, "products."/"inventory.products" → sucedido por "items.").
    private static readonly string[] LegacyFragments =
    [
        "purchasing", "expenses",
        "inventory.products", "products.",
    ];

    [Fact]
    public void Permissions_have_no_duplicate_values()
    {
        var permissions = KernelRegistry.Permissions;

        permissions.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Navigation_permission_keys_exist_in_Permissions_registry()
    {
        var allowed = new HashSet<string>(KernelRegistry.Permissions, StringComparer.Ordinal);

        var orphanKeys = KernelRegistry.Navigation
            .Where(n => n.PermissionKey is not null)
            .Select(n => n.PermissionKey!)
            .Where(key => !allowed.Contains(key))
            .Distinct()
            .ToList();

        orphanKeys.Should().BeEmpty("todo permiso referenciado por navegación debe existir en KernelRegistry.Permissions");
    }

    [Fact]
    public void Navigation_items_have_unique_ids_and_route_paths()
    {
        var navigation = KernelRegistry.Navigation;

        navigation.Select(n => n.Id).Should().OnlyHaveUniqueItems();
        navigation.Select(n => (n.GroupId, n.RoutePath)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Modules_settings_group_preserves_legacy_guid()
    {
        var settings = KernelRegistry.Modules.Single(m => m.Code == "settings");

        settings.GroupId.Should().Be(Guid.Parse("f2d0ca10-0000-4000-8000-000000000008"));
    }

    [Fact]
    public void Navigation_items_preserve_legacy_guids_for_company_and_companies()
    {
        var byRoute = KernelRegistry.Navigation.ToDictionary(n => n.RoutePath, n => n.Id);

        byRoute["/settings/company"].Should().Be(Guid.Parse("00000000-0000-4000-8000-000000000101"));
        byRoute["/companies"].Should().Be(Guid.Parse("00000000-0000-4000-8000-000000000104"));
    }

    [Fact]
    public void Permissions_and_routes_have_no_legacy_module_fragments()
    {
        var keys = KernelRegistry.Permissions
            .Concat(KernelRegistry.Navigation.Select(n => n.RoutePath))
            .Concat(KernelRegistry.Modules.Select(m => m.Code));

        foreach (var key in keys)
        {
            foreach (var fragment in LegacyFragments)
            {
                key.Should().NotContain(fragment, $"'{key}' no debe contener el fragmento legacy '{fragment}'");
            }
        }
    }
}
