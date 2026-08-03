using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// P0-02 Fase 4 — administración limitada de <c>CompanyFinancialDestination</c> (diseño §6.4,
/// §6.4ter, §20.2): expone únicamente los 4 casos de uso aprobados (crear, renombrar, cambiar
/// cuenta contable, activar/desactivar). Sin CRUD genérico, sin <c>PUT</c> de reemplazo completo,
/// sin <c>DELETE</c> — los 8 campos estructurales de un destino ya creado son inmutables
/// (§6.4ter), ningún endpoint de esta superficie los acepta.
/// </summary>
[AppFeature(
    "Destinos Financieros",
    $"perm:{SettingsPermissions.FinancialDestinationsView}",
    "🏦",
    "/settings/financial-destinations",
    null,
    45,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/finance/financial-destinations")]
[Authorize]
[Produces("application/json")]
public sealed class CompanyFinancialDestinationController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyFinancialDestinationController(IMediator mediator) => _mediator = mediator;

    /// <summary>P0-02 Fase 13 Remediación 01 — listado para el selector de destino financiero y la administración limitada de la fase.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.FinancialDestinationsView}")]
    public async Task<IActionResult> GetList(
        [FromQuery] bool? isActive,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(new GetCompanyFinancialDestinationListQuery(isActive), ct), "OK");

    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.FinancialDestinationsManage}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyFinancialDestinationCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct));

    [HttpPost("{id:guid}/rename")]
    [Authorize(Policy = $"perm:{SettingsPermissions.FinancialDestinationsManage}")]
    public async Task<IActionResult> Rename(
        Guid id,
        [FromBody] RenameCompanyFinancialDestinationRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdateCompanyFinancialDestinationNameCommand(id, body.Name),
                ct
            )
        );

    [HttpPost("{id:guid}/change-accounting-account")]
    [Authorize(Policy = $"perm:{SettingsPermissions.FinancialDestinationsManage}")]
    public async Task<IActionResult> ChangeAccountingAccount(
        Guid id,
        [FromBody] ChangeCompanyFinancialDestinationAccountingAccountRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ChangeCompanyFinancialDestinationAccountingAccountCommand(
                    id,
                    body.AccountingAccountId
                ),
                ct
            )
        );

    [HttpPost("{id:guid}/set-active")]
    [Authorize(Policy = $"perm:{SettingsPermissions.FinancialDestinationsManage}")]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] SetCompanyFinancialDestinationActiveRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SetCompanyFinancialDestinationActiveCommand(id, body.IsActive),
                ct
            )
        );
}

// ── Contratos HTTP mínimos — cada uno acepta exclusivamente el campo que su caso de uso permite
// mutar (§6.4ter); ninguno expone Code/DestinationTypeCode/CurrencyCode/CashRegisterId/campos
// bancarios post-creación. ────────────────────────────────────────────────

public sealed record RenameCompanyFinancialDestinationRequest(string Name);

public sealed record ChangeCompanyFinancialDestinationAccountingAccountRequest(
    Guid AccountingAccountId
);

public sealed record SetCompanyFinancialDestinationActiveRequest(bool IsActive);
