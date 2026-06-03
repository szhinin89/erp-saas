using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Attributes;
using ERP.Application.Configuration.DTOs;
using ERP.Application.Configuration.UseCases.GetSubscriberBillingProfile;
using ERP.Application.Configuration.UseCases.UpsertSubscriberBillingProfile;

namespace ERP.API.Controllers;

/// <summary>
/// ConfiguraciÃ³n de la representaciÃ³n impresa (RIDE) y datos de facturaciÃ³n por tenant.
/// Un Ãºnico registro por tenant: PUT siempre crea o actualiza (upsert).
/// </summary>
[AppFeature("ConfiguraciÃ³n RIDE", "perm:settings.ride.view", "ðŸ–¨ï¸", "/settings/ride", "perm:settings.group", 32)]
[ApiController]
[Route("api/settings/ride")]
[Authorize]
[Produces("application/json")]
public sealed class RideSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RideSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Retorna la configuraciÃ³n RIDE/facturaciÃ³n del tenant, o null si no estÃ¡ configurada.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:settings.ride.view")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberBillingProfileDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSubscriberBillingProfileQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Crea o actualiza la configuraciÃ³n RIDE/facturaciÃ³n del tenant.</summary>
    [HttpPut]
    [Authorize(Policy = "perm:settings.ride.edit")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberBillingProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertSubscriberBillingProfileCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Guardado");
    }
}
