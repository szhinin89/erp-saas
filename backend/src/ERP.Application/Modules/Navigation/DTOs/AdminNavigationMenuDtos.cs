namespace ERP.Application.Navigation.DTOs;

/// <summary>Árbol de menú para edición SuperAdmin (incluye ids y jerarquía recursiva).</summary>
public sealed record AdminNavigationMenuResponse(IReadOnlyList<AdminNavGroupRowDto> Groups);

public sealed record AdminNavGroupRowDto(
    Guid Id,
    string Code,
    string Icon,
    string LabelKey,
    int SortOrder,
    string? ModuleKey,
    IReadOnlyList<string>? Roles,
    bool RequireSuperAdminPanel,
    bool IsActive,
    IReadOnlyList<AdminNavItemRowDto> RootItems);

public sealed record AdminNavItemRowDto(
    Guid Id,
    Guid? ParentItemId,
    string RoutePath,
    string LabelKey,
    int SortOrder,
    string? ModuleKey,
    string? PermissionKey,
    IReadOnlyList<string>? PermissionKeysAny,
    bool IsActive,
    IReadOnlyList<AdminNavItemRowDto> Children);

/// <summary>Un nivel de hermanos: mismo grupo y mismo padre (null = raíz del grupo).</summary>
public sealed record NavItemSiblingOrderDto(
    Guid GroupId,
    Guid? ParentItemId,
    IReadOnlyList<Guid> OrderedItemIds);
