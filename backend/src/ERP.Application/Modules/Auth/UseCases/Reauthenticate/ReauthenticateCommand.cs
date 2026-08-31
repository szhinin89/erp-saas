using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.Reauthenticate;

/// <summary>
/// <paramref name="RawRefreshToken"/> viene siempre de la cookie httpOnly (resuelto en el
/// controller, igual que Refresh/Logout) — nunca del body, para que el modal de reautenticación
/// no pueda usarse para "reautenticar" con el refresh token de otro usuario.
/// </summary>
public record ReauthenticateCommand(string RawRefreshToken, string Password)
    : IRequest<Result<AuthResponseDto>>;
