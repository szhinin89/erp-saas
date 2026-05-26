using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Configuration.DTOs;
using ERP.Application.Configuration.UseCases.GetSriSettings;
using ERP.Application.Configuration.UseCases.UpsertSriSettings;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// GestiÃ³n de la configuraciÃ³n SRI por tenant.
/// Un Ãºnico registro por tenant: PUT siempre crea o actualiza (upsert).
/// El secuencial actual NO se resetea al actualizar.
/// </summary>
[AppFeature("Configuración SRI", "perm:settings.sri.view", "🧾", "/settings/sri", "perm:settings.group", 31)]
[ApiController]
[Route("api/settings/sri")]
[Authorize]
[Produces("application/json")]
public sealed class SriSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SriSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Retorna la configuraciÃ³n SRI del tenant autenticado, o null si no estÃ¡ configurada.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:settings.sri.view")]
    [ProducesResponseType(typeof(ApiResponse<SriConfigurationDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSriConfigurationQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>
    /// Crea o actualiza la configuración SRI del tenant.
    /// </summary>
    /// <response code="200">ConfiguraciÃ³n guardada.</response>
    /// <response code="400">Datos invÃ¡lidos.</response>
    [HttpPut]
    [Authorize(Policy = "perm:settings.sri.edit")]
    [ProducesResponseType(typeof(ApiResponse<SriConfigurationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertSriConfigurationCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Guardado");
    }
}

