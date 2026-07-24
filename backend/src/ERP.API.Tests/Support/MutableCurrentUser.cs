using ERP.Application.Common;

namespace ERP.API.Tests.Support;

internal sealed class MutableCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; }

    public bool IsAuthenticated => UserId != Guid.Empty;

    public string? Username { get; set; } = "integration.user";

    public string? Email { get; set; } = "integration@test.local";

    public string? FullName { get; set; } = "Integration User";

    public string? Role { get; set; } = "Admin";
}
