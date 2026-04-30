namespace ERP.Application.Access.UseCases.Permissions;

public record UpsertProfilePermissionsCommand(
    Guid ProfileId,
    IReadOnlyList<PermissionUpsertItem> Items
);

public record PermissionUpsertItem(
    string PermissionKey,
    bool IsAllowed
);

