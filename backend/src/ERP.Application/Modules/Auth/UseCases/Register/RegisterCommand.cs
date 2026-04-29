namespace ERP.Application.Auth.UseCases.Register;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid TenantId,
    string Role = "User"
);
