namespace ERP.Application.Auth.UseCases.SuperAdminLogin;

public record SuperAdminLoginCommand(
    string Email,
    string Password
);

