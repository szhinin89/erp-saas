namespace ERP.Application.Access.DTOs;

public record ProfilePermissionsDto(Guid ProfileId, IReadOnlyList<ProfilePermissionItemDto> Items);

public record ProfilePermissionItemDto(string PermissionKey, bool IsAllowed);
