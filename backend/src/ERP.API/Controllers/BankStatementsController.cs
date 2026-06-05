using ERP.API.Contracts;
using ERP.API.Contracts.Cash;
using ERP.API.Extensions;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.UseCases;
using ERP.Domain.Modules.Cash;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/cash/bank")]
[Authorize]
[Produces("application/json")]
public sealed class BankStatementsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStatementParser _parser;

    public BankStatementsController(IMediator mediator, IStatementParser parser)
    {
        _mediator = mediator;
        _parser = parser;
    }

    [HttpGet("extractos")]
    [Authorize(Policy = "perm:cash.bank.view")]
    public async Task<IActionResult> ListStatements([FromQuery] Guid cuentaBancariaId, CancellationToken ct)
    {
        var r = await _mediator.Send(new ListStatementsByAccountQuery(cuentaBancariaId), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<BankStatementDto>());
    }

    [HttpGet("extractos/{id:guid}")]
    [Authorize(Policy = "perm:cash.bank.view")]
    public async Task<IActionResult> GetStatement(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetStatementDetailQuery(id), ct);
        return this.ToOkOrBadRequest(r);
    }

    [HttpPost("extractos/importar")]
    [Authorize(Policy = "perm:cash.bank.create")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportStatement([FromForm] ImportStatementForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new ApiResponse<object>(false, "Empty file.", new { }));

        await using var stream = form.File.OpenReadStream();
        IReadOnlyList<StatementParseRow> movs;
        try
        {
            movs = await _parser.ParseAsync(stream, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>(false, $"Could not read file: {ex.Message}", new { }));
        }

        var cmd = new ImportarBankStatementCommand(
            form.BankAccountId,
            form.PeriodFrom,
            form.PeriodTo,
            form.OpeningBalance,
            form.ClosingBalance,
            movs);

        var r = await _mediator.Send(cmd, ct);
        return this.ToCreatedOrBadRequest(r, "Importado");
    }

    [HttpGet("extractos/{StatementId:guid}/sugerencias-conciliacion")]
    [Authorize(Policy = "perm:cash.bank.view")]
    public async Task<IActionResult> SuggestReconciliation(Guid StatementId, CancellationToken ct)
    {
        var r = await _mediator.Send(new SuggestReconciliationQuery(StatementId), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<ReconciliationSuggestionDto>());
    }

    [HttpPost("movimientos/{movimientoId:guid}/conciliar")]
    [Authorize(Policy = "perm:cash.bank.conciliar")]
    public async Task<IActionResult> ReconcileTransaction(Guid movimientoId, [FromBody] ReconcileRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ReconcileBankTransactionCommand(movimientoId, body.JournalEntryId), ct);
        return this.ToOkOrBadRequest(r, "Reconciled", () => false);
    }
}
