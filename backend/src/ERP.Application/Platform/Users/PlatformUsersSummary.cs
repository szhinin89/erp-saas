namespace ERP.Application.Platform.Users;

public sealed record PlatformUserListItem(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PlatformRole,
    bool IsActive);

public sealed record PlatformUsersPage(
    IReadOnlyList<PlatformUserListItem> Users,
    int ActivePlatformUsers);

public interface IPlatformUsersReader
{
    Task<PlatformUsersPage> ListAsync(CancellationToken ct = default);
}
