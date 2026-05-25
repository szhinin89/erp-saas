namespace ERP.Application.Access.UseCases.SubscriberAccess;

public record SubscriberCompanyUserMembershipItemDto(
    Guid IdentityUserId,
    string Email,
    string FullName,
    string Role,
    Guid? ProfileId,
    bool IsActive
);

