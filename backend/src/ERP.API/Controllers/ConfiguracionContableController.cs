using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Application.Modules.Contabilidad.UseCases.ConfiguracionContable;

namespace ERP.API.Controllers;

/// <summary>Configuración de cuentas por defecto por tenant (compras, ventas, IVA, caja, mapeo de gastos).</summary>
[ApiController]
[Route("api/contabilidad/configuracion")]
[Authorize]
[Produces("application/json")]
public sealed class ConfiguracionContableController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConfiguracionContableController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:accounting.config.view")]
    [ProducesResponseType(typeof(ApiResponse<ConfiguracionContableEmpresaDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetConfiguracionContableQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPut]
    [Authorize(Policy = "perm:accounting.config.edit")]
    [ProducesResponseType(typeof(ApiResponse<ConfiguracionContableEmpresaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertConfiguracionContableCommand command,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Guardado");
    }

    [HttpGet("gastos")]
    [Authorize(Policy = "perm:accounting.config.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfiguracionGastoCategoriaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGastos(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetConfiguracionGastoCategoriasQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ConfiguracionGastoCategoriaDto>());
    }

    [HttpPost("gastos")]
    [Authorize(Policy = "perm:accounting.config.edit")]
    [ProducesResponseType(typeof(ApiResponse<ConfiguracionGastoCategoriaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateGastoMapping(
        [FromBody] CreateConfiguracionGastoCategoriaCommand command,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Creado");
    }

    [HttpDelete("gastos/{id:guid}")]
    [Authorize(Policy = "perm:accounting.config.edit")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteGastoMapping(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteConfiguracionGastoCategoriaCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Eliminado");
    }
}
