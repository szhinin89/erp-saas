using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Kernel;
using ERP.Domain.Kernel.Navigation;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Access.UseCases.Permissions;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 / NAV-HIERARCHY-UNIFY-01 — construye el catálogo puramente en
/// memoria desde <see cref="KernelRegistry.Modules"/>/<see cref="KernelRegistry.Navigation"/>: sin
/// acceso a BD, sin nombre de pantalla/permiso hardcodeado. Agregar un <c>[NavItem]</c> nuevo con
/// <c>Permission</c> lo hace aparecer aquí automáticamente, sin tocar este archivo.
///
/// Jerarquía de 4 niveles — Grupo (módulo) → Categoría → Pantalla → Acciones — construida sobre
/// la misma relación <see cref="NavigationItemDefinition.ParentItemId"/> que ya usa
/// <c>NavigationBuilder</c> para el árbol del menú: una Categoría es cualquier ítem de primer
/// nivel del módulo (<c>ParentItemId == null</c>) sin <see cref="NavigationItemDefinition.PermissionKey"/>
/// propio (contenedor puro, visible solo vía <c>PermissionsAnyCsv</c>); sus Pantallas son los
/// ítems hijos con <c>PermissionKey</c> real. Ningún permiso individual asignable vive fuera de
/// una categoría: si algún <c>[NavItem]</c> nuevo se registrara sin contenedor padre (bug de
/// convención, no debería ocurrir tras NAV-HIERARCHY-UNIFY-01), cae en una categoría de respaldo
/// "Gestión" sintética en vez de perderse o quedar suelto en el catálogo.
/// </summary>
[AdminReadModel("Catálogo de permisos asignables derivado del Kernel Registry.")]
public sealed class GetPermissionCatalogHandler
    : IRequestHandler<GetPermissionCatalogQuery, Result<PermissionCatalogDto>>
{
    private static readonly IReadOnlyDictionary<string, (string Label, string Description)> ActionVerbs =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["create"] = ("Crear", "Permite crear nuevos registros."),
            ["update"] = ("Actualizar", "Permite editar registros existentes."),
            ["edit"] = ("Actualizar", "Permite editar registros existentes."),
            ["delete"] = ("Eliminar", "Permite eliminar registros."),
            ["disable"] = ("Deshabilitar", "Permite deshabilitar registros."),
            ["deactivate"] = ("Desactivar", "Permite desactivar registros."),
            ["activate"] = ("Activar", "Permite activar registros."),
            ["confirm"] = ("Confirmar", "Permite confirmar la operación."),
            ["cancel"] = ("Cancelar", "Permite cancelar la operación."),
            ["reverse"] = ("Reversar", "Permite reversar la operación."),
            ["manage"] = ("Administrar", "Permite administrar por completo esta pantalla."),
            ["configure"] = ("Configurar", "Permite configurar esta pantalla."),
            ["configure-company"] = ("Configurar empresa", "Permite configurar datos de empresa asociados."),
            ["close"] = ("Cerrar", "Permite cerrar la operación."),
            ["open"] = ("Abrir", "Permite abrir la operación."),
            ["record"] = ("Registrar", "Permite registrar movimientos."),
            ["retry"] = ("Reintentar", "Permite reintentar la operación."),
            ["detail"] = ("Ver detalle", "Permite ver el detalle de un registro."),
            ["assign_temporary_password"] = (
                "Asignar contraseña temporal",
                "Permite asignar una contraseña temporal."
            ),
        };

    public Task<Result<PermissionCatalogDto>> HandleAsync(
        CancellationToken cancellationToken = default
    ) => Handle(new GetPermissionCatalogQuery(), cancellationToken);

    public Task<Result<PermissionCatalogDto>> Handle(
        GetPermissionCatalogQuery request,
        CancellationToken cancellationToken
    )
    {
        var navigationByModule = KernelRegistry.Navigation.ToLookup(n => n.GroupCode);

        var groups = KernelRegistry
            .Modules.Select(m => new PermissionCatalogGroupDto(
                m.Code,
                $"app.nav.group.{m.Code}",
                m.SortOrder,
                BuildCategories(m.Code, navigationByModule[m.Code].ToList())
            ))
            .Where(g => g.Categories.Count > 0)
            .OrderBy(g => g.SortOrder)
            .ToList();

        return Task.FromResult(Result<PermissionCatalogDto>.Success(new PermissionCatalogDto(groups)));
    }

    private static IReadOnlyList<PermissionCatalogCategoryDto> BuildCategories(
        string moduleCode,
        IReadOnlyList<NavigationItemDefinition> moduleItems
    )
    {
        var categories = new List<PermissionCatalogCategoryDto>();

        var topLevelContainers = moduleItems
            .Where(n => n.ParentItemId is null && n.PermissionKey is null)
            .OrderBy(n => n.SortOrder);

        foreach (var container in topLevelContainers)
        {
            // Recorre todos los descendientes del contenedor, no solo hijos directos — una
            // categoría puede envolver un sub-contenedor propio (p. ej. "Empresa" envuelve al
            // contenedor "Empresas", que a su vez agrupa "Mis empresas"/"Datos de la empresa").
            var items = CollectDescendantScreens(moduleItems, container.Id)
                .OrderBy(n => n.SortOrder)
                .Select(BuildItem)
                .ToList();

            if (items.Count > 0)
                categories.Add(
                    new PermissionCatalogCategoryDto(container.Id, container.LabelKey, container.SortOrder, items)
                );
        }

        // Respaldo defensivo: un ítem con permiso real que quedó sin categoría contenedora
        // (no debería ocurrir tras NAV-HIERARCHY-UNIFY-01) no se pierde ni queda suelto — se
        // agrupa en una categoría sintética "Gestión" propia del módulo.
        var strayItems = moduleItems
            .Where(n => n.ParentItemId is null && n.PermissionKey is not null)
            .OrderBy(n => n.SortOrder)
            .Select(BuildItem)
            .ToList();

        if (strayItems.Count > 0)
            categories.Add(
                new PermissionCatalogCategoryDto(
                    DeterministicGuid($"permission-catalog-fallback-category:{moduleCode}"),
                    "permissionsAssignment.fallbackCategory",
                    int.MaxValue,
                    strayItems
                )
            );

        return categories;
    }

    private static IEnumerable<NavigationItemDefinition> CollectDescendantScreens(
        IReadOnlyList<NavigationItemDefinition> moduleItems,
        Guid parentId
    )
    {
        foreach (var child in moduleItems.Where(n => n.ParentItemId == parentId))
        {
            if (child.PermissionKey is not null)
                yield return child;
            else
                foreach (var descendant in CollectDescendantScreens(moduleItems, child.Id))
                    yield return descendant;
        }
    }

    private static Guid DeterministicGuid(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash[..16]);
    }

    private static PermissionCatalogItemDto BuildItem(NavigationItemDefinition item)
    {
        var actions = new List<PermissionCatalogActionDto>
        {
            new(item.PermissionKey!, "Ver / Acceder", "Permite ver y acceder a esta pantalla.", 0),
        };

        var relatedKeys = item.RelatedActionPermissionKeys ?? Array.Empty<string>();
        for (var i = 0; i < relatedKeys.Count; i++)
        {
            var key = relatedKeys[i];
            var (label, description) = ResolveVerbLabel(key);
            actions.Add(new PermissionCatalogActionDto(key, label, description, i + 1));
        }

        return new PermissionCatalogItemDto(
            item.Id,
            item.LabelKey,
            item.RoutePath,
            item.PermissionKey!,
            item.SortOrder,
            actions,
            item.FeatureKey,
            item.RequiresExternalEntitlement
        );
    }

    private static (string Label, string Description) ResolveVerbLabel(string permissionKey)
    {
        var verb = permissionKey[(permissionKey.LastIndexOf('.') + 1)..];
        return ActionVerbs.TryGetValue(verb, out var resolved)
            ? resolved
            : (verb, $"Permite realizar la acción '{verb}'.");
    }
}
