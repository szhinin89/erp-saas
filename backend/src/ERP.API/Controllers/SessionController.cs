using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Session.DTOs;
using ERP.Application.Modules.Session.UseCases.GetMyAvailableBranches;
using ERP.Application.Modules.Session.UseCases.GetSessionContext;
using ERP.Application.Modules.Session.UseCases.SwitchBranch;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/session")]
[Produces("application/json")]
public sealed class SessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Devuelve el contexto de la sesión actual.
    /// </summary>
    /// <remarks>
    /// Incluye identidad del usuario, tenant, autorización, preferencias y la sucursal activa (nula si no hay una).
    /// La respuesta no se almacena en caché (<c>Cache-Control: no-store</c>).
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contexto de la sesión.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [HttpGet("context")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<SessionContextDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetContext(CancellationToken cancellationToken)
    {
        Response.Headers["Cache-Control"] = "no-store";

        var result = await _mediator.Send(new GetSessionContextQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Cambia la sucursal activa de la sesión.
    /// </summary>
    /// <remarks>
    /// Valida el acceso del usuario a la sucursal con la misma guarda de empresa y sucursal del resto del sistema;
    /// si no la supera, la operación falla con el código y el status de esa guarda.
    /// </remarks>
    /// <param name="request">Id de la sucursal a activar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sucursal activa actualizada.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [HttpPost("switch-branch")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<SessionBranchDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SwitchBranch(
        [FromBody] SwitchBranchRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new SwitchBranchCommand(request.BranchId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Lista las sucursales que el usuario puede activar.
    /// </summary>
    /// <remarks>
    /// Corresponde a las sucursales de la empresa activa que el usuario puede activar. La respuesta no se almacena
    /// en caché (<c>Cache-Control: no-store</c>).
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sucursales disponibles.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [HttpGet("available-branches")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<MyAvailableBranchesDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailableBranches(CancellationToken cancellationToken)
    {
        Response.Headers["Cache-Control"] = "no-store";

        var result = await _mediator.Send(new GetMyAvailableBranchesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
