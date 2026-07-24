using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public record ResetPasswordWithTokenCommand(
    string Token,
    string NewPassword,
    Guid? TenantId) : IRequest<Result<bool>>;
