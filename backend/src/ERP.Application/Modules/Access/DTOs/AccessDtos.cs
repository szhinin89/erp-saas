namespace ERP.Application.Access.DTOs;

public record AccessibleTenantDto(
    Guid TenantId,
    string Name,
    string Slug,
    string Role
);

public record BootstrapLoginResponseDto(
    Guid UserId,
    string FullName,
    string Email,
    string BootstrapToken,
    IReadOnlyList<AccessibleTenantDto> Tenants
);

public record SessionResponseDto(
    Guid UserId,
    string FullName,
    string Email,
    Guid TenantId,
    string Role,
    string Token,
    string? PlanCode,
    IReadOnlyList<string> EnabledModules
);

public record SuperAdminTenantItemDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    string? PlanCode,
    IReadOnlyList<string> EnabledModules,
    bool HasModuleRestrictions
);

