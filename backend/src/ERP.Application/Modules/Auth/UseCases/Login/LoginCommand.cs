using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.Login;

/// <summary>
/// <c>TerminalId</c> es opcional y hoy no lo envía ningún cliente real — no existe todavía un
/// mecanismo de identificación de dispositivo (<c>ITerminalIdentifier</c> nunca se implementó
/// en este roadmap, es trabajo de frontend explícitamente fuera de alcance). Cuando ese
/// mecanismo exista, un caller real podrá pasarlo sin cambios adicionales en LoginHandler.
/// </summary>
public record LoginCommand(
    string Username,
    string Password,
    string? TerminalId = null
) : IRequest<Result<AuthResponseDto>>;
