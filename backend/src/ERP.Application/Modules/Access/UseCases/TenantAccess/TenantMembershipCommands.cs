namespace ERP.Application.Access.UseCases.TenantAccess;

public record TenantUpsertMembershipCommand(
    string Email,
    string Role,
    Guid? ProfileId,
    string? FirstName,
    string? LastName,
    string? Password
);

public record TenantRevokeMembershipCommand(string Email);

