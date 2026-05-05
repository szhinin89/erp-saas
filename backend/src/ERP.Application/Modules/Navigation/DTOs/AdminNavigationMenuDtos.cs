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
    string? DisplayLabel,
    int SortOrder,
    string? ModuleKey,
    string? PermissionKey,
    IReadOnlyList<string>? PermissionKeysAny,
    bool IsActive,
    Guid? SaasFeatureDefinitionId,
    IReadOnlyList<AdminNavItemRowDto> Children);

/// <summary>
/// Un nivel de hermanos: mismo grupo y mismo padre (null = raíz del grupo).
/// En conjunto, todos los niveles definen el árbol completo (padre + orden entre hermanos).
/// </summary>
public sealed record NavItemSiblingOrderDto(
    Guid GroupId,
    Guid? ParentItemId,
    IReadOnlyList<Guid> OrderedItemIds);

/// <summary>Alta de ítem de menú bajo un grupo (raíz o bajo un padre del mismo grupo).</summary>
public sealed record CreateNavItemRequest(
    Guid GroupId,
    Guid? ParentItemId,
    string RoutePath,
    string DisplayLabel,
    string? ModuleKey,
    string? PermissionKey,
    Guid? SaasFeatureDefinitionId = null);
