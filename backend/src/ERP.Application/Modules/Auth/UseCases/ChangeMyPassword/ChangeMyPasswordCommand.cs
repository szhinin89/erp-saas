using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.ChangeMyPassword;

/// <summary>
/// Cambio de contraseña por un usuario ya autenticado, sobre sí mismo — no acepta TenantId/
/// CompanyId ni UserId del cliente, opera exclusivamente sobre ICurrentUser.UserId. Distinto del
/// flujo de recuperación anónimo (ResetPasswordWithTokenCommand): aquí se exige la contraseña
/// actual en vez de un token, pero reutiliza el mismo mecanismo de aplicación
/// (IdentityUser.SetPasswordHash) y de invalidación (IRefreshTokenService.RevokeAllForUserAsync).
/// </summary>
public sealed record ChangeMyPasswordCommand(
    string CurrentPassword,
    string NewPassword
) : IRequest<Result<bool>>;
