namespace ERP.Application.Navigation.DTOs;

public sealed record SessionMenuItemDto(
    string RoutePath,
    string LabelKey,
    int SortOrder,
    string? ModuleKey,
    string? PermissionKey,
    IReadOnlyList<string>? PermissionKeysAny,
    IReadOnlyList<SessionMenuItemDto>? Children = null);

public sealed record SessionMenuGroupDto(
    string Code,
    string Icon,
    string LabelKey,
    int SortOrder,
    string? ModuleKey,
    IReadOnlyList<string>? Roles,
    bool RequireSuperAdminPanel,
    IReadOnlyList<SessionMenuItemDto> Items);
