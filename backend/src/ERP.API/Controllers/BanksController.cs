using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.MasterData.UseCases.Banks;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// BANK-CATALOG-01: catálogo maestro de bancos — Configuración > Catálogos > Bancos. CRUD básico,
// sin cuenta contable ni relación con PaymentMethodAccount (eso pertenece a un módulo Tesorería/
// Bancos futuro, fuera de este ticket).
[AppFeature(
    "Bancos",
    $"perm:{SettingsPermissions.BanksView}",
    "🏦",
    "/settings/catalogs/banks",
    null,
    66
)]
[ApiController]
[Route("api/v1/settings/catalogs/banks")]
[Authorize]
[Produces("application/json")]
public sealed class BanksController : ControllerBase
{
    private readonly IMediator _mediator;

    public BanksController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksView}")]
    public async Task<IActionResult> List(
        [FromQuery] bool onlyActive = false,
        [FromQuery] string? search = null,
        CancellationToken ct = default
    ) => Ok(await _mediator.Send(new ListBanksQuery(onlyActive, search), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksView}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetBankByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksCreate}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateBankCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct), "bank");

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksUpdate}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBankCommand cmd,
        CancellationToken ct
    )
    {
        if (id != cmd.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksManage}")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableBankCommand(id), ct), "Activado.");

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksManage}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableBankCommand(id), ct), "Desactivado.");
}
