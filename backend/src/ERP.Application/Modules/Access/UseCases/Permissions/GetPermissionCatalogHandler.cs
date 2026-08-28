using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Kernel;
using ERP.Domain.Kernel.Navigation;
using MediatR;

namespace ERP.Application.Access.UseCases.Permissions;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 — construye el catálogo puramente en memoria desde
/// <see cref="KernelRegistry.Modules"/>/<see cref="KernelRegistry.Navigation"/>: sin acceso a BD,
/// sin nombre de pantalla/permiso hardcodeado. Agregar un <c>[NavItem]</c> nuevo con
/// <c>Permission</c> lo hace aparecer aquí automáticamente, sin tocar este archivo.
///
/// Solo se incluyen ítems con <see cref="ERP.Domain.Kernel.Navigation.NavigationItemDefinition.PermissionKey"/>
/// no nulo — los contenedores puros de menú (que solo usan <c>PermissionsAnyCsv</c>, un OR de
/// visibilidad) no son un permiso individual asignable. Esto da naturalmente la estructura
/// Grupo → Pantalla → Acciones de 2 niveles pedida, sin los contenedores intermedios del menú.
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
        var itemsByGroup = KernelRegistry
            .Navigation.Where(n => n.PermissionKey is not null)
            .GroupBy(n => n.GroupCode);

        var groups = KernelRegistry
            .Modules.Join(
                itemsByGroup,
                m => m.Code,
                g => g.Key,
                (m, g) =>
                    new PermissionCatalogGroupDto(
                        m.Code,
                        $"app.nav.group.{m.Code}",
                        m.SortOrder,
                        g.OrderBy(n => n.SortOrder)
                            .Select(BuildItem)
                            .ToList()
                    )
            )
            .Where(g => g.Items.Count > 0)
            .OrderBy(g => g.SortOrder)
            .ToList();

        return Task.FromResult(Result<PermissionCatalogDto>.Success(new PermissionCatalogDto(groups)));
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
            actions
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
