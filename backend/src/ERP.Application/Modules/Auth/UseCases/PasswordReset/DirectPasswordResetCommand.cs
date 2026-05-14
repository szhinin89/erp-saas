using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Auth.UseCases.PasswordReset;

/// <summary>Reset inmediato (modo Direct del tenant). Mantener por compatibilidad; el flujo público usa token por email.</summary>
public record DirectPasswordResetCommand(
    Guid TenantId,
    string Email,
    string NewPassword
) : IRequest<Result<bool>>;
