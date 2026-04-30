namespace ERP.Application.Auth.UseCases.PasswordReset;

public record PasswordResetCommand(
    Guid TenantId,
    string Email,
    string NewPassword
);

