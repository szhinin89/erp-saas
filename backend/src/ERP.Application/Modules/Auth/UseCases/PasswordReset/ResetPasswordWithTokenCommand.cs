namespace ERP.Application.Auth.UseCases.PasswordReset;

public record ResetPasswordWithTokenCommand(
    string Token,
    string NewPassword,
    Guid? TenantId);
