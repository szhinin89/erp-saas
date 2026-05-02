namespace ERP.Application.Access.DTOs;

public record MyPermissionsDto(
    IReadOnlyList<string> Permissions,
    string? PlanCode,
    IReadOnlyList<string> EnabledModules
);

