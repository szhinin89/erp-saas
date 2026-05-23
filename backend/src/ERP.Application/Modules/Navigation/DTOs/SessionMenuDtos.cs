namespace ERP.Application.Navigation.DTOs;

public sealed record SessionMenuItemDto(
    string RoutePath,
    string LabelKey,
    string? DisplayLabel,
    int SortOrder,
    string? ModuleKey,
    string? PermissionKey,
    IReadOnlyList<string>? PermissionKeysAny,
    IReadOnlyList<string>? ItemRoles = null,
    IReadOnlyList<SessionMenuItemDto>? Children = null,
    string? Icon = null);

/// <param name="MenuBarLayout">Opcional: <c>horizontal</c> o <c>vertical</c> (constructor de menú por plan/empresa).</param>
public sealed record SessionMenuGroupDto(
    string Code,
    string Icon,
    string LabelKey,
    int SortOrder,
    string? ModuleKey,
    IReadOnlyList<string>? Roles,
    bool RequirePlatformPanel,
    IReadOnlyList<SessionMenuItemDto> Items,
    string? MenuBarLayout = null)
{
    /// <summary>Alias JSON canónico — mismo valor que <see cref="RequirePlatformPanel"/>.</summary>
    public bool RequirePlatformPanel => RequirePlatformPanel;
}
