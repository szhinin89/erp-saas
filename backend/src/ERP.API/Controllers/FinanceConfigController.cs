using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Configuración de cuentas por defecto por tenant (compras, ventas, IVA, caja, mapeo de gastos).</summary>
[AppFeature("Config. contable", "perm:finance.config.view", "⚙️", "/finance/config", "perm:finance.accounts.view", 25)]
[ApiController]
[Route("api/finance/config")]
[Authorize]
[Produces("application/json")]
public sealed class FinanceConfigController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinanceConfigController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:finance.config.view")]
    [ProducesResponseType(typeof(ApiResponse<AccountingSetupDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetConfigurationContableQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPut]
    [Authorize(Policy = "perm:finance.config.edit")]
    [ProducesResponseType(typeof(ApiResponse<AccountingSetupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertConfigurationContableCommand command,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Guardado");
    }

    [HttpGet("gastos")]
    [Authorize(Policy = "perm:finance.config.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetExpenseCategorysQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ExpenseCategoryDto>());
    }

    [HttpPost("gastos")]
    [Authorize(Policy = "perm:finance.config.edit")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateExpenseMapping(
        [FromBody] CreateExpenseCategoryCommand command,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Creado");
    }

    [HttpDelete("gastos/{id:guid}")]
    [Authorize(Policy = "perm:finance.config.edit")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteExpenseMapping(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteExpenseCategoryCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Eliminado");
    }
}


