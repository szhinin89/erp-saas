using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Setup.CreateInitialAdmin;
using ERP.Application.Setup.GetSetupStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>First-run ERP initialization endpoints. Both actions are anonymous and token-gated.</summary>
[ApiController]
[Route("api/v1/setup")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class SetupController : ControllerBase
{
    private readonly IMediator _mediator;

    public SetupController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Consulta si la instalación inicial ya se completó.
    /// </summary>
    /// <remarks>
    /// Operación pública del flujo de primera ejecución. No exige ni revela el token de instalación.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Estado de la instalación inicial.</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<SetupStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSetupStatusQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea el administrador inicial de la instalación.
    /// </summary>
    /// <remarks>
    /// Operación pública protegida por el token de instalación de un solo uso (no acepta tenant ni rol del
    /// cliente). Solo funciona mientras la instalación no esté inicializada: si ya lo está responde 409 y, con
    /// token inválido, 401. Este endpoint forma parte del flujo first-run que completa el propietario.
    /// </remarks>
    /// <param name="command">Token de instalación y datos del administrador inicial.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Administrador inicial creado.</response>
    /// <response code="401">El token de instalación es inválido (envelope `UNAUTHORIZED`). No exige JWT: es un rechazo del caso de uso.</response>
    /// <response code="409">La instalación ya fue inicializada.</response>
    [HttpPost("admin")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateInitialAdmin(
        [FromBody] CreateInitialAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        // El handler clasifica el fallo (CONFLICT ya inicializado, UNAUTHORIZED token inválido);
        // el status sale de la tabla única, nunca de comparar el texto del error.
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
