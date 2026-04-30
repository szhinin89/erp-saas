namespace ERP.Application.Auth.UseCases.Login;

public record LoginCommand(
    string Email,
    string Password
);
