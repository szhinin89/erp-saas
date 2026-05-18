using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Attributes;
using ERP.Application.Configuration.DTOs;
using ERP.Application.Configuration.UseCases.GetBillingSettings;
using ERP.Application.Configuration.UseCases.UpsertBillingSettings;

namespace ERP.API.Controllers;

/// <summary>
/// Configuración de la representación impresa (RIDE) y datos de facturación por tenant.
/// Un único registro por tenant: PUT siempre crea o actualiza (upsert).
/// </summary>
[AppFeature("Configuración RIDE", "perm:settings.ride.view", "🖨️", "/settings/ride", "perm:settings.group", 32)]
[ApiController]
[Route("api/settings/ride")]
[Authorize]
[Produces("application/json")]
public sealed class RideSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RideSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Retorna la configuración RIDE/facturación del tenant, o null si no está configurada.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:settings.ride.view")]
    [ProducesResponseType(typeof(ApiResponse<BillingSettingsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBillingSettingsQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Crea o actualiza la configuración RIDE/facturación del tenant.</summary>
    [HttpPut]
    [Authorize(Policy = "perm:settings.ride.edit")]
    [ProducesResponseType(typeof(ApiResponse<BillingSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertBillingSettingsCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Guardado");
    }
}
