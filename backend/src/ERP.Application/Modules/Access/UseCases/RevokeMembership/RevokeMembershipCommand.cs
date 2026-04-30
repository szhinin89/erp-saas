namespace ERP.Application.Access.UseCases.RevokeMembership;

public record RevokeMembershipCommand(
    Guid TenantId,
    string UserEmail
);

