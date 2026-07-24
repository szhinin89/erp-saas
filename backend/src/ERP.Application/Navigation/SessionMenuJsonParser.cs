using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>Serializa / deserializa el menú de sesión (mismo shape que <see cref="SessionMenuGroupDto"/>[]),
/// aceptando también nodos «constructor» con <c>nombre</c>, <c>icono</c>, <c>ruta</c>, <c>permiso</c>, <c>hijos</c>.</summary>
public static class SessionMenuJsonParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string Serialize(IReadOnlyList<SessionMenuGroupDto> groups)
        => JsonSerializer.Serialize(groups, JsonOptions);

    public static bool TryDeserialize(string? json, out IReadOnlyList<SessionMenuGroupDto>? groups)
    {
        groups = null;
        var t = json?.Trim();
        if (string.IsNullOrEmpty(t))
            return false;

        try
        {
            var rows = JsonSerializer.Deserialize<List<MenuGroupJson>>(t, JsonOptions);
            if (rows is null || rows.Count == 0)
                return false;

            var mapped = new List<SessionMenuGroupDto>(rows.Count);
            foreach (var g in rows)
            {
                if (string.IsNullOrWhiteSpace(g.Code) || string.IsNullOrWhiteSpace(g.LabelKey))
                    return false;
                var items = MapItems(g.Items);
                if (items is null)
                    return false;
                mapped.Add(new SessionMenuGroupDto(
                    g.Code.Trim(),
                    (g.Icon ?? string.Empty).Trim(),
                    g.LabelKey.Trim(),
                    g.SortOrder,
                    string.IsNullOrWhiteSpace(g.ModuleKey) ? null : g.ModuleKey.Trim(),
                    g.Roles is { Count: > 0 } ? g.Roles : null,
                    g.RequirePlatformPanel,
                    items,
                    NormalizeMenuBarLayout(g.MenuBarLayout)));
            }

            groups = mapped;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizeMenuBarLayout(string? raw)
    {
        var t = raw?.Trim().ToLowerInvariant();
        return t is "horizontal" or "vertical" ? t : null;
    }

    private static IReadOnlyList<SessionMenuItemDto>? MapItems(List<MenuItemJson>? items)
    {
        if (items is null)
            return Array.Empty<SessionMenuItemDto>();
        var list = new List<SessionMenuItemDto>(items.Count);
        for (var idx = 0; idx < items.Count; idx++)
        {
            var mapped = MapOneItem(items[idx], idx);
            if (mapped is null)
                return null;
            list.Add(mapped);
        }

        return list;
    }

    private static SessionMenuItemDto? MapOneItem(MenuItemJson? i, int sortOrder)
    {
        if (i is null)
            return null;

        var childSource = i.Hijos ?? i.Children;
        var children = MapItems(childSource);
        if (children is null)
            return null;

        var nombre = (i.Name ?? string.Empty).Trim();
        var labelKey = (i.LabelKey ?? string.Empty).Trim();
        var isBuilderShape = string.IsNullOrEmpty(labelKey) && nombre.Length > 0;

        if (isBuilderShape)
        {
            var ruta = (i.Path ?? string.Empty).Trim();
            var permiso = (i.Permission ?? string.Empty).Trim();
            var leaf = ruta.Length > 0 && permiso.Length > 0;
            var folder = ruta.Length == 0 && permiso.Length == 0;

            if (!leaf && !folder)
                return null;

            if (leaf && children.Count > 0)
                return null;

            var idSuffix = string.IsNullOrWhiteSpace(i.BuilderId)
                ? Guid.NewGuid().ToString("n")[..12]
                : SanitizeId(i.BuilderId!);

            var icon = FirstNonEmpty(i.Icon, i.Icon);
            if (leaf)
            {
                var lk = $"nav.planLeaf.{SlugPerm(permiso)}";
                return new SessionMenuItemDto(
                    ruta,
                    lk,
                    nombre,
                    sortOrder,
                    string.IsNullOrWhiteSpace(i.ModuleKey) ? null : i.ModuleKey.Trim(),
                    permiso,
                    i.PermissionKeysAny is { Count: > 0 } ? i.PermissionKeysAny : null,
                    i.ItemRoles is { Count: > 0 } ? i.ItemRoles : null,
                    null,
                    string.IsNullOrWhiteSpace(icon) ? null : icon.Trim());
            }

            // carpeta
            var folderLabelKey = $"nav.planFolder.{idSuffix}";
            return new SessionMenuItemDto(
                string.Empty,
                folderLabelKey,
                nombre,
                sortOrder,
                string.IsNullOrWhiteSpace(i.ModuleKey) ? null : i.ModuleKey.Trim(),
                null,
                i.PermissionKeysAny is { Count: > 0 } ? i.PermissionKeysAny : null,
                i.ItemRoles is { Count: > 0 } ? i.ItemRoles : null,
                children.Count > 0 ? children : null,
                string.IsNullOrWhiteSpace(icon) ? null : icon.Trim());
        }

        // Legacy: exige labelKey
        if (string.IsNullOrWhiteSpace(i.LabelKey))
            return null;

        var keysAny = i.PermissionKeysAny is { Count: > 0 } ? i.PermissionKeysAny : null;
        var roles = i.ItemRoles is { Count: > 0 } ? i.ItemRoles : null;
        var routePath = (i.RoutePath ?? string.Empty).Trim();
        var permKey = string.IsNullOrWhiteSpace(i.PermissionKey) ? null : i.PermissionKey.Trim();
        var iconLegacy = FirstNonEmpty(i.Icon, i.Icon);
        return new SessionMenuItemDto(
            routePath,
            i.LabelKey.Trim(),
            string.IsNullOrWhiteSpace(i.DisplayLabel) ? null : i.DisplayLabel.Trim(),
            sortOrder,
            string.IsNullOrWhiteSpace(i.ModuleKey) ? null : i.ModuleKey.Trim(),
            permKey,
            keysAny,
            roles,
            children.Count > 0 ? children : null,
            string.IsNullOrWhiteSpace(iconLegacy) ? null : iconLegacy.Trim());
    }

    private static string SanitizeId(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0)
            return Guid.NewGuid().ToString("n")[..12];
        var chars = s.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').Take(48).ToArray();
        return chars.Length > 0 ? new string(chars) : Guid.NewGuid().ToString("n")[..12];
    }

    private static string SlugPerm(string permiso)
    {
        var chars = permiso.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        var s = new string(chars).Trim('_');
        while (s.Contains("__", StringComparison.Ordinal))
            s = s.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrEmpty(s) ? "item" : s;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            var t = v?.Trim();
            if (!string.IsNullOrEmpty(t))
                return t;
        }

        return null;
    }

    private sealed class MenuGroupJson
    {
        public string Code { get; set; } = "";
        public string Icon { get; set; } = "";
        public string LabelKey { get; set; } = "";
        public int SortOrder { get; set; }
        public string? ModuleKey { get; set; }
        public List<string>? Roles { get; set; }
        public bool RequirePlatformPanel { get; set; }
        public List<MenuItemJson>? Items { get; set; }
        public string? MenuBarLayout { get; set; }
    }

    private sealed class MenuItemJson
    {
        public string RoutePath { get; set; } = "";
        public string LabelKey { get; set; } = "";
        public string? DisplayLabel { get; set; }
        public int SortOrder { get; set; }
        public string? ModuleKey { get; set; }
        public string? PermissionKey { get; set; }
        public List<string>? PermissionKeysAny { get; set; }
        public List<string>? ItemRoles { get; set; }
        public List<MenuItemJson>? Children { get; set; }

        public string? Icon { get; set; }

        [JsonPropertyName("nombre")]
        public string? Name { get; set; }

        [JsonPropertyName("icono")]
        public string? Icono { get; set; }

        [JsonPropertyName("ruta")]
        public string? Path { get; set; }

        [JsonPropertyName("permiso")]
        public string? Permission { get; set; }

        [JsonPropertyName("hijos")]
        public List<MenuItemJson>? Hijos { get; set; }

        [JsonPropertyName("id")]
        public string? BuilderId { get; set; }
    }
}
