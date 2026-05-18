using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.UseCases;
using ERP.Domain.Modules.Cash;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>ConciliaciÃ³n bancaria, caja chica y flujo de efectivo (tenant).</summary>
[AppFeature("Caja y bancos", "perm:cash.bank.view", "ðŸ¦", "/cash/bank", null, 60)]
[ApiController]
[Route("api/cash/bank")]
[Authorize]
[Produces("application/json")]
public sealed class CashBankController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStatementParser _parser;

    public CashBankController(IMediator mediator, IStatementParser parser)
    {
        _mediator = mediator;
        _parser   = parser;
    }

    [HttpGet("cuentas-bancarias")]
    [Authorize(Policy = "perm:cash.bank.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BankAccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBankAccounts(CancellationToken ct)
    {
        var r = await _mediator.Send(new ListCuentasBancariasQuery(), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<BankAccountDto>());
    }

    [HttpPost("cuentas-bancarias")]
    [Authorize(Policy = "perm:cash.bank.create")]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBankAccount([FromBody] CreateBankAccountRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CrearBankAccountCommand(
                body.Name,
                body.AccountNumber,
                body.AccountType,
                body.Currency,
                body.InitialBalance,
                body.LedgerAccountId),
            ct);
        return this.ToCreatedOrBadRequest(r, "Creado");
    }

    [HttpGet("extractos")]
    [Authorize(Policy = "perm:cash.bank.view")]
    public async Task<IActionResult> ListStatements([FromQuery] Guid cuentaBancariaId, CancellationToken ct)
    {
        var r = await _mediator.Send(new ListExtractosPorCuentaQuery(cuentaBancariaId), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<BankStatementDto>());
    }

    [HttpGet("extractos/{id:guid}")]
    [Authorize(Policy = "perm:cash.bank.view")]
    public async Task<IActionResult> GetStatement(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetExtractoDetalleQuery(id), ct);
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

    [HttpGet("extractos/{extractoId:guid}/sugerencias-conciliacion")]
    [Authorize(Policy = "perm:cash.bank.view")]
    public async Task<IActionResult> SuggestReconciliation(Guid extractoId, CancellationToken ct)
    {
        var r = await _mediator.Send(new SugerirReconciliationQuery(extractoId), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<ReconciliationSuggestionDto>());
    }

    public sealed record ConciliarRequest(Guid JournalEntryId);

    [HttpPost("movimientos/{movimientoId:guid}/conciliar")]
    [Authorize(Policy = "perm:cash.bank.conciliar")]
    public async Task<IActionResult> ReconcileTransaction(Guid movimientoId, [FromBody] ConciliarRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ConciliarBankTransactionCommand(movimientoId, body.JournalEntryId), ct);
        return this.ToOkOrBadRequest(r, "Reconciled", () => false);
    }

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
            new CrearGastoPettyCashCommand(
                body.PettyCashId,
                body.Date,
                body.Description,
                body.Amount,
                body.VoucherType,
                body.VoucherNumber),
            ct);
        return this.ToCreatedOrBadRequest(r, "Registrado");
    }

    [HttpPost("caja-chica/arqueos")]
    [Authorize(Policy = "perm:cash.bank.arqueos.perform")]
    public async Task<IActionResult> CreateCashCount([FromBody] CreateCashCountRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CrearCashCountCommand(body.PettyCashId, body.CountDate, body.PhysicalCash, body.Notes),
            ct);
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

    [HttpGet("flujo-efectivo/real")]
    [Authorize(Policy = "perm:cash.bank.flujo.view")]
    public async Task<IActionResult> GetActualCashFlow([FromQuery] DateTime desde, [FromQuery] DateTime hasta, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetFlujoEfectivoRealQuery(desde, hasta), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<DailyCashFlowDto>());
    }

    public sealed class ImportStatementForm
    {
        public IFormFile File { get; set; } = null!;
        public Guid BankAccountId { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
    }

    public sealed record CreateBankAccountRequest(
        string Name,
        string AccountNumber,
        string AccountType,
        string Currency,
        decimal InitialBalance,
        Guid? LedgerAccountId);

    public sealed record CreatePettyCashRequest(
        string Name,
        decimal AssignedBalance,
        Guid? ReplenishmentBankAccountId,
        Guid? LedgerCashAccountId);

    public sealed record CreatePettyCashExpenseRequest(
        Guid PettyCashId,
        DateTime Date,
        string Description,
        decimal Amount,
        string VoucherType,
        string? VoucherNumber);

    public sealed record CreateCashCountRequest(
        Guid PettyCashId,
        DateTime CountDate,
        decimal PhysicalCash,
        string? Notes);

    public sealed record PettyCashReplenishmentRequest(Guid PettyCashId, decimal Amount);
}



