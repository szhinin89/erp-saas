using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Navigation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ERP.Domain.Kernel;

/// <summary>
/// Punto de ensamblado único del Platform Kernel — source of truth absoluto.
/// <see cref="Modules"/>, <see cref="Permissions"/> y <see cref="Navigation"/> se
/// derivan por reflexión de las clases <c>[Module]</c>/<c>[NavItem]</c> y de los
/// catálogos <c>*Permissions</c> en <c>ERP.Domain.Kernel</c>. No hay seeders ni
/// configuración externa: cualquier cambio de permisos/navegación/módulos se hace
/// editando estas clases.
/// </summary>
public static class KernelRegistry
{
    private static readonly Assembly KernelAssembly = typeof(KernelRegistry).Assembly;

    public static IReadOnlyList<ModuleDefinition> Modules { get; } = BuildModules();

    public static IReadOnlyList<string> Permissions { get; } = BuildPermissions();

    public static IReadOnlyList<NavigationItemDefinition> Navigation { get; } = BuildNavigation();

    /// <summary>
    /// ADMIN-PERMISSIONS-SSOT-KERNEL-02: unión de <see cref="NavigationItemDefinition.PermissionKey"/>
    /// (uno por cada NavItem con permiso de acceso real, excluyendo contenedores que solo usan
    /// <c>PermissionsAnyCsv</c>) y <see cref="NavigationItemDefinition.RelatedActionPermissionKeys"/>
    /// de cada ítem. Fuente única para el catálogo de permisos asignables
    /// (<c>GetPermissionCatalogHandler</c>) y para rechazar claves desconocidas al guardar permisos
    /// de perfil (<c>UpsertProfilePermissionsHandler</c>) — un solo cálculo, dos consumidores.
    /// </summary>
    public static IReadOnlySet<string> AssignablePermissionKeys { get; } = BuildAssignablePermissionKeys();

    private static List<ModuleDefinition> BuildModules()
    {
        return KernelAssembly
            .GetTypes()
            .Select(t => (Type: t, Attr: t.GetCustomAttribute<ModuleAttribute>()))
            .Where(x => x.Attr is not null)
            .Select(x => new ModuleDefinition(
                x.Attr!.Code,
                x.Attr.GroupId is not null
                    ? Guid.Parse(x.Attr.GroupId)
                    : DeterministicGuid($"module:{x.Attr.Code}"),
                x.Attr.Icon,
                x.Attr.SortOrder
            ))
            .OrderBy(m => m.SortOrder)
            .ToList();
    }

    private static List<string> BuildPermissions()
    {
        return KernelAssembly
            .GetTypes()
            .Where(t =>
                t.IsClass
                && t.IsAbstract
                && t.IsSealed
                && t.Namespace == "ERP.Domain.Kernel.Permissions"
            )
            .SelectMany(t =>
                t.GetFields(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy
                )
            )
            .Where(f => f.FieldType == typeof(string) && f.IsLiteral && !f.IsInitOnly)
            .Select(f => (string)f.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
    }

    private static List<NavigationItemDefinition> BuildNavigation()
    {
        var modulesByCode = Modules.ToDictionary(m => m.Code);
        var items = new List<NavigationItemDefinition>();

        foreach (var type in KernelAssembly.GetTypes())
        {
            var moduleAttr = type.GetCustomAttribute<ModuleAttribute>();
            if (moduleAttr is null)
                continue;

            var module = modulesByCode[moduleAttr.Code];

            foreach (
                var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy
                )
            )
            {
                if (field.FieldType != typeof(string) || !field.IsLiteral || field.IsInitOnly)
                    continue;

                var navAttr = field.GetCustomAttribute<NavItemAttribute>();
                if (navAttr is null)
                    continue;

                var routePath = (string)field.GetRawConstantValue()!;
                var id = navAttr.Id is not null
                    ? Guid.Parse(navAttr.Id)
                    : DeterministicGuid($"navitem:{module.Code}:{routePath}");

                items.Add(
                    new NavigationItemDefinition(
                        id,
                        module.GroupId,
                        module.Code,
                        routePath,
                        navAttr.LabelKey,
                        navAttr.Permission,
                        navAttr.SortOrder,
                        navAttr.ParentId is not null ? Guid.Parse(navAttr.ParentId) : null,
                        navAttr.PermissionsAnyCsv is not null
                            ? JsonSerializer.Serialize(
                                navAttr.PermissionsAnyCsv.Split(
                                    ',',
                                    StringSplitOptions.TrimEntries
                                        | StringSplitOptions.RemoveEmptyEntries
                                )
                            )
                            : null,
                        SplitCsv(navAttr.RelatedActionPermissionsCsv)
                    )
                );
            }
        }

        return items.OrderBy(i => i.GroupId).ThenBy(i => i.SortOrder).ToList();
    }

    private static IReadOnlyList<string>? SplitCsv(string? csv) =>
        csv is null
            ? null
            : csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static HashSet<string> BuildAssignablePermissionKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Navigation)
        {
            if (item.PermissionKey is not null)
                keys.Add(item.PermissionKey);
            if (item.RelatedActionPermissionKeys is not null)
                keys.UnionWith(item.RelatedActionPermissionKeys);
        }
        return keys;
    }

    private static Guid DeterministicGuid(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash[..16]);
    }
}
