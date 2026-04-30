namespace ERP.Application.Security.DTOs;

public record SecurityUserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive
);

