using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.CompletePasswordReset;

/// <summary>
/// Fase H — cierra el flujo de primer inicio de sesión: consume el PasswordResetToken emitido
/// por LoginHandler cuando IdentityUser.RequirePasswordReset estaba activo, fija la nueva
/// contraseña y devuelve una sesión completa (JWT + refresh), igual que un login exitoso.
/// </summary>
public sealed record CompletePasswordResetCommand(string PasswordResetToken, string NewPassword)
    : IRequest<Result<AuthResponseDto>>;
