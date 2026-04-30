namespace ERP.Application.Access.UseCases.BootstrapLogin;

public record BootstrapLoginCommand(
    string Email,
    string Password
);

