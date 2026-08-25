using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Application.Modules.Accounting.UseCases.Accounts;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Accounting.UseCases.PostingRules;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Accounting.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Accounting;

/// <summary>
/// Administración del Plan de Cuentas / Períodos Contables / Reglas de Contabilización
/// (ADR-026). Desde ACCOUNTING-LEDGER-VISIBILITY-01 también expone lectura de JournalEntry
/// (listado paginado, detalle con líneas, búsqueda por documento de origen) — el Posting Engine
/// (§8) y el motor de contabilización ya existen y producen asientos reales (Post()/Reverse(),
/// Fase 5.2-5.4); esta fase solo agrega visibilidad, nunca Create/Update/Delete de JournalEntry.
/// Sin verbo DELETE — la baja lógica (Enable/Disable) usa PATCH, decisión deliberada frente al
/// patrón de otros módulos que sí usan DELETE para baja lógica (ver Architecture Review Board
/// Fase 2.2). Los reportes de solo lectura (Libro Diario/Mayor/Balance de Comprobación/Estado de
/// Resultados/Balance General) viven en <see cref="AccountingReportsController"/> desde
/// ACCOUNTING-FINANCIAL-STATEMENTS-10 — split mecánico para no seguir creciendo este archivo,
/// mismo patrón ya usado en Purchases.
/// </summary>
[AppFeature("Contabilidad", $"perm:{AccountingPermissions.View}", "📒", "/accounting", null, 60)]
[ApiController]
[Route("api/v1/accounting")]
[Authorize]
[Produces("application/json")]
public sealed class AccountingController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountingController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // ACCOUNTS (Chart of Accounts)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("accounts")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccounts(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetAccountsQuery(), ct), "OK");

    [HttpGet("accounts/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountByIdQuery(id), ct));

    [HttpGet("accounts/by-code/{code}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountByCode(string code, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountByCodeQuery(code), ct));

    [HttpPost("accounts")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    public async Task<IActionResult> CreateAccount(
        [FromBody] CreateAccountCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPatch("accounts/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Update}")]
    public async Task<IActionResult> UpdateAccount(
        Guid id,
        [FromBody] UpdateAccountCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpPatch("accounts/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Update}")]
    public async Task<IActionResult> EnableAccount(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableAccountCommand(id), ct));

    [HttpPatch("accounts/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Delete}")]
    public async Task<IActionResult> DisableAccount(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableAccountCommand(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // ACCOUNTING PERIODS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("accounting-periods")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountingPeriods(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetAccountingPeriodsQuery(), ct), "OK");

    [HttpGet("accounting-periods/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetAccountingPeriodById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountingPeriodByIdQuery(id), ct));

    [HttpPost("accounting-periods")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    public async Task<IActionResult> CreateAccountingPeriod(
        [FromBody] CreateAccountingPeriodCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPatch("accounting-periods/{id:guid}/close")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Update}")]
    public async Task<IActionResult> CloseAccountingPeriod(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CloseAccountingPeriodCommand(id), ct));

    [HttpPatch("accounting-periods/{id:guid}/lock")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Update}")]
    public async Task<IActionResult> LockAccountingPeriod(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new LockAccountingPeriodCommand(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // POSTING RULES
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("posting-rules")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetPostingRules(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPostingRulesQuery(), ct), "OK");

    [HttpGet("posting-rules/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetPostingRuleById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPostingRuleByIdQuery(id), ct));

    [HttpPost("posting-rules")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    public async Task<IActionResult> CreatePostingRule(
        [FromBody] CreatePostingRuleCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPatch("posting-rules/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Update}")]
    public async Task<IActionResult> UpdatePostingRule(
        Guid id,
        [FromBody] UpdatePostingRuleCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpPatch("posting-rules/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Update}")]
    public async Task<IActionResult> EnablePostingRule(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnablePostingRuleCommand(id), ct));

    [HttpPatch("posting-rules/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Delete}")]
    public async Task<IActionResult> DisablePostingRule(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisablePostingRuleCommand(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // JOURNAL ENTRIES (solo lectura — ACCOUNTING-LEDGER-VISIBILITY-01)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("journal-entries")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetJournalEntries(
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        [FromQuery] JournalEntryStatus? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] string? sourceModule,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetJournalEntriesQuery(
                    pageNumber <= 0 ? 1 : pageNumber,
                    pageSize <= 0 ? 20 : pageSize,
                    status,
                    fromDate,
                    toDate,
                    sourceModule
                ),
                ct
            ),
            "OK"
        );

    [HttpGet("journal-entries/{id:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetJournalEntryById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetJournalEntryByIdQuery(id), ct));

    [HttpGet("journal-entries/by-source/{sourceModule}/{sourceDocumentId:guid}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    public async Task<IActionResult> GetJournalEntriesBySource(
        string sourceModule,
        Guid sourceDocumentId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetJournalEntriesBySourceQuery(sourceModule, sourceDocumentId),
                ct
            ),
            "OK"
        );

}
