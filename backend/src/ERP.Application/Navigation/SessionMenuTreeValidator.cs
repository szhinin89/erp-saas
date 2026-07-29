using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>
/// Reglas: hoja = ruta y permiso no vacíos, sin hijos; carpeta = ambos vacíos, con al menos un hijo;
/// no se admite ruta sin permiso ni al revés.
/// </summary>
public static class SessionMenuTreeValidator
{
    public static void Validate(IReadOnlyList<SessionMenuGroupDto> groups)
    {
        foreach (var g in groups)
            ValidateItems(g.Items, g.Code);
    }

    private static void ValidateItems(IReadOnlyList<SessionMenuItemDto> items, string groupCode)
    {
        foreach (var item in items)
        {
            var name = item.DisplayLabel?.Trim();
            if (string.IsNullOrEmpty(name))
                name = item.LabelKey.Trim();
            if (string.IsNullOrEmpty(name))
                name = "?";

            var route = (item.RoutePath ?? string.Empty).Trim();
            var perm = (item.PermissionKey ?? string.Empty).Trim();
            var leaf = route.Length > 0 && perm.Length > 0;
            var folder = route.Length == 0 && perm.Length == 0;
            var ch = item.Children;

            if (leaf && ch is { Count: > 0 })
                throw new InvalidOperationException(
                    $"Menú ({groupCode}): el ítem «{name}» es hoja (tiene ruta y permiso) pero contiene subítems."
                );

            if (folder && (ch is null || ch.Count == 0))
                throw new InvalidOperationException(
                    $"Menú ({groupCode}): el ítem «{name}» es carpeta (sin ruta ni permiso) pero no tiene hijos."
                );

            if (!leaf && !folder)
                throw new InvalidOperationException(
                    $"Menú ({groupCode}): el ítem «{name}» es inconsistente (falta ruta o permiso en la hoja)."
                );

            if (ch is { Count: > 0 })
                ValidateItems(ch, groupCode);
        }
    }
}
