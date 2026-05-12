using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Configuration.DTOs;
using ERP.Application.Configuration.UseCases.GetConfiguracionSRI;
using ERP.Application.Configuration.UseCases.UpsertConfiguracionSRI;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión de la configuración SRI por tenant.
/// Un único registro por tenant: PUT siempre crea o actualiza (upsert).
/// El secuencial actual NO se resetea al actualizar.
/// </summary>
[Modulo("Configuración SRI", "perm:ventas.configuracion.view", "🧾", "/configuracion/sri", null, 35)]
[ApiController]
[Route("api/configuracion-sri")]
[Authorize]
[Produces("application/json")]
public sealed class ConfiguracionSRIController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConfiguracionSRIController(IMediator mediator) => _mediator = mediator;

    /// <summary>Retorna la configuración SRI del tenant autenticado, o null si no está configurada.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:ventas.configuracion.view")]
    [ProducesResponseType(typeof(ApiResponse<ConfiguracionSRIDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetConfiguracionSRIQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>
    /// Crea o actualiza la configuración SRI del tenant.
    /// El SecuencialActual se preserva al actualizar para no romper la numeración en curso.
    /// </summary>
    /// <response code="200">Configuración guardada.</response>
    /// <response code="400">Datos inválidos.</response>
    [HttpPut]
    [Authorize(Policy = "perm:ventas.configuracion.edit")]
    [ProducesResponseType(typeof(ApiResponse<ConfiguracionSRIDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertConfiguracionSRICommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Guardado");
    }
}
