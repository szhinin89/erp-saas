namespace ERP.Application.Auth.DTOs;

public record RegisterDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid TenantId
);

public record LoginDto(
    string Email,
    string Password
);

public record AuthResponseDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    Guid TenantId,
    string Token,
    string? PlanCode,
    IReadOnlyList<string> EnabledModules
);
