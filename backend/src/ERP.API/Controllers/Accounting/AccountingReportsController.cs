using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.UseCases.Reports;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Accounting;

/// <summary>
/// Reportes contables de solo lectura (ACCOUNTING-REPORTS-09 / ACCOUNTING-FINANCIAL-
/// STATEMENTS-10) — Libro Diario, Libro Mayor, Balance de Comprobación, Estado de Resultados,
/// Balance General. Extraído de <see cref="AccountingController"/> (que ya excedía el umbral de
/// advertencia del guardrail de arquitectura) siguiendo el mismo patrón de split ya establecido
/// para Purchases (<c>PurchasesController</c> + <c>PurchaseCreditNoteController</c> +
/// <c>PurchaseReturnController</c>) — mismo prefijo de ruta base, sin <c>[AppFeature]</c> propio
/// (el nav ya lo registra <see cref="AccountingController"/>, evitar una entrada duplicada). Solo
/// lectura sobre <c>JournalEntry</c>/<c>JournalEntryLine</c> ya <c>Posted</c> — nunca recalcula
/// desde Ventas/Compras/Inventario/Finanzas, nunca modifica asientos históricos.
/// </summary>
[ApiController]
[Route("api/v1/accounting/reports")]
[Authorize]
[Produces("application/json")]
public sealed class AccountingReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountingReportsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("general-journal")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetGeneralJournalReport(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] string? sourceModule,
        [FromQuery] string? search,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetGeneralJournalReportQuery(
                    fromDate,
                    toDate,
                    sourceModule,
                    search,
                    pageNumber <= 0 ? 1 : pageNumber,
                    pageSize <= 0 ? 50 : pageSize
                ),
                ct
            ),
            "OK"
        );

    [HttpGet("general-ledger")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetGeneralLedgerReport(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? accountId,
        [FromQuery] string? accountCodeFrom,
        [FromQuery] string? accountCodeTo,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetGeneralLedgerReportQuery(
                    fromDate,
                    toDate,
                    accountId,
                    accountCodeFrom,
                    accountCodeTo
                ),
                ct
            ),
            "OK"
        );

    [HttpGet("trial-balance")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetTrialBalanceReport(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] bool includeZeroMovementAccounts,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetTrialBalanceReportQuery(fromDate, toDate, includeZeroMovementAccounts),
                ct
            ),
            "OK"
        );

    [HttpGet("income-statement")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetIncomeStatementReport(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetIncomeStatementReportQuery(fromDate, toDate), ct),
            "OK"
        );

    [HttpGet("balance-sheet")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetBalanceSheetReport(
        [FromQuery] DateOnly asOfDate,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetBalanceSheetReportQuery(asOfDate), ct),
            "OK"
        );
}
