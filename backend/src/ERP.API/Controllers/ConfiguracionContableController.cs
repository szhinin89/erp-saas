using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>ConfiguraciÃ³n de cuentas por defecto por tenant (compras, ventas, IVA, caja, mapeo de gastos).</summary>
[Modulo("Config. contable", "perm:accounting.config.view", "âš™ï¸", "/contabilidad/configuracion", "perm:accounting.accounts.view", 25)]
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
    [ProducesResponseType(typeof(ApiResponse<AccountingSetupDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetConfiguracionContableQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPut]
    [Authorize(Policy = "perm:accounting.config.edit")]
    [ProducesResponseType(typeof(ApiResponse<AccountingSetupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertConfiguracionContableCommand command,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Guardado");
    }

    [HttpGet("gastos")]
    [Authorize(Policy = "perm:accounting.config.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGastos(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetExpenseCategorysQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ExpenseCategoryDto>());
    }

    [HttpPost("gastos")]
    [Authorize(Policy = "perm:accounting.config.edit")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateGastoMapping(
        [FromBody] CreateExpenseCategoryCommand command,
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
        var result = await _mediator.Send(new DeleteExpenseCategoryCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Eliminado");
    }
}


