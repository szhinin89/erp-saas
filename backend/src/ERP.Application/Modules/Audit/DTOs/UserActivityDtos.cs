namespace ERP.Application.Audit.DTOs;

public record UserActivityDto(
    Guid Id,
    string Module,
    string Action,
    string? EntityType,
    Guid? EntityId,
    string? Description,
    DateTime CreatedAt,
    string? UserEmail,
    string? UserFullName);

