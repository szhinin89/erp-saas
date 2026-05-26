using ERP.API.Contracts.Cash;
using ERP.API.Extensions;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.UseCases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/cash/bank")]
[Authorize]
[Produces("application/json")]
public sealed class PettyCashController : ControllerBase
{
    private readonly IMediator _mediator;

    public PettyCashController(IMediator mediator) => _mediator = mediator;

    [HttpGet("cajas-chicas")]
    [Authorize(Policy = "perm:cash.bank.cajachica.view")]
    public async Task<IActionResult> ListPettyCashes(CancellationToken ct)
    {
        var r = await _mediator.Send(new ListCashesChicasQuery(), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<PettyCashDto>());
    }

    [HttpPost("cajas-chicas")]
    [Authorize(Policy = "perm:cash.bank.cajachica.create")]
    public async Task<IActionResult> CreatePettyCash([FromBody] CreatePettyCashRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CrearPettyCashCommand(body.Name, body.AssignedBalance, body.ReplenishmentBankAccountId, body.LedgerCashAccountId),
            ct);
        return this.ToCreatedOrBadRequest(r, "Creado");
    }

    [HttpPost("caja-chica/gastos")]
    [Authorize(Policy = "perm:cash.bank.cajachica.edit")]
    public async Task<IActionResult> CreatePettyCashExpense([FromBody] CreatePettyCashExpenseRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CreateExpensePettyCashCommand(body.PettyCashId, body.Date, body.Description, body.Amount, body.VoucherType, body.VoucherNumber),
            ct);
        return this.ToCreatedOrBadRequest(r, "Registrado");
    }

    [HttpPost("caja-chica/arqueos")]
    [Authorize(Policy = "perm:cash.bank.arqueos.perform")]
    public async Task<IActionResult> CreateCashCount([FromBody] CreateCashCountRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new CrearCashCountCommand(body.PettyCashId, body.CountDate, body.PhysicalCash, body.Notes), ct);
        return this.ToCreatedOrBadRequest(r, "Creado");
    }

    [HttpPost("caja-chica/arqueos/{arqueoId:guid}/aprobar")]
    [Authorize(Policy = "perm:cash.bank.arqueos.perform")]
    public async Task<IActionResult> ApproveCashCount(Guid arqueoId, CancellationToken ct)
    {
        var r = await _mediator.Send(new AprobarCashCountCommand(arqueoId), ct);
        return this.ToOkOrBadRequest(r, "IsApproved");
    }

    [HttpPost("caja-chica/reposicion")]
    [Authorize(Policy = "perm:cash.bank.cajachica.edit")]
    public async Task<IActionResult> Replenish([FromBody] PettyCashReplenishmentRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ReposicionPettyCashCommand(body.PettyCashId, body.Amount), ct);
        return this.ToOkOrBadRequest(r, "OK");
    }
}
