using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public record ResetPasswordWithTokenCommand(
    string Token,
    string NewPassword,
    Guid? TenantId) : IRequest<Result<bool>>;
