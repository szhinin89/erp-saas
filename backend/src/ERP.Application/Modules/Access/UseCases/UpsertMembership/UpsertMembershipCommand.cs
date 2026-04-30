namespace ERP.Application.Access.UseCases.UpsertMembership;

public record UpsertMembershipCommand(
    Guid TenantId,
    string UserEmail,
    string Role,
    Guid? ProfileId = null
);

