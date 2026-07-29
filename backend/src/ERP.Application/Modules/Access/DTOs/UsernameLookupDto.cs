namespace ERP.Application.Access.DTOs;

public sealed record UsernameMembershipLookupDto(
    Guid CompanyUserMembershipId,
    bool IsActive,
    string Role,
    Guid? ProfileId
);

public sealed record UsernameLookupDto(
    bool IdentityUserExists,
    string? FullName,
    UsernameMembershipLookupDto? Membership
);
