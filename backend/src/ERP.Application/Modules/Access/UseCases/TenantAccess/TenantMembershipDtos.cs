namespace ERP.Application.Access.UseCases.TenantAccess;

public record TenantMembershipItemDto(
    Guid IdentityUserId,
    string Email,
    string FullName,
    string Role,
    Guid? ProfileId,
    bool IsActive
);

