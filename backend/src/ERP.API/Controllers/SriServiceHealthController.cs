using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSriServiceHealth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Salud técnica de los servicios SRI (Recepción/Autorización) para el ambiente de la empresa actual.
/// Visible para cualquier usuario autenticado de la empresa — mismo criterio que
/// <c>GET electronic-invoicing/status</c>: no expone datos sensibles. Solo lee el monitor en memoria:
/// nunca ejecuta SOAP, ping ni envíos.
/// </summary>
[ApiController]
[Route("api/v1/electronic-invoicing/sri-health")]
[Authorize]
[Produces("application/json")]
public sealed class SriServiceHealthController : ControllerBase
{
    private readonly IMediator _mediator;

    public SriServiceHealthController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Salud técnica de los servicios del SRI.
    /// </summary>
    /// <remarks>
    /// Estado de Recepción y Autorización observado de las comunicaciones existentes (monitor en memoria). No hace
    /// llamadas nuevas al SRI.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Salud de los servicios del SRI.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SriServiceHealthDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetSriServiceHealthQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
