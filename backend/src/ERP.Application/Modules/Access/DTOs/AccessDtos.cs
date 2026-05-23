namespace ERP.Application.Access.DTOs;

public record AccessibleSubscriberDto(
    Guid SubscriberId,
    string Name,
    string Slug,
    string Role
);

public record BootstrapLoginResponseDto(
    Guid UserId,
    string FullName,
    string Email,
    string BootstrapToken,
    IReadOnlyList<AccessibleSubscriberDto> Subscribers
);

public record SessionResponseDto(
    Guid UserId,
    string FullName,
    string Email,
    Guid SubscriberId,
    string Role,
    string Token,
    string? PlanCode,
    IReadOnlyList<string> EnabledModules)
{
    public Guid? CompanyId { get; init; }
}

public record PlatformSubscriberItemDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    string? PlanCode,
    IReadOnlyList<string> EnabledModules,
    bool HasModuleRestrictions,
    bool HasCustomMenu,
    DateTime CreatedAt,
    int TotalUsers,
    int ActiveUsers);

