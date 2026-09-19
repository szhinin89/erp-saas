using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — catálogo administrable de motivos de movimiento manual de
/// caja (SSOT dinámico), scope obligatorio Tenant+Company. Consumido por el formulario "Registrar
/// movimiento manual de efectivo" en /treasury/cash. No borra físico: Toggle solo activa/desactiva.
/// </summary>
[AppFeature(
    "Motivos de caja",
    $"perm:{CajaPermissions.View}",
    "list_alt",
    "/treasury/cash/reasons",
    null,
    71
)]
[ApiController]
[Route("api/v1/cash-movement-reasons")]
[Authorize]
[Produces("application/json")]
public sealed class CashMovementReasonsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashMovementReasonsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> List(
        [FromQuery] string? movementType = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCashMovementReasonsQuery(movementType, includeInactive), ct),
            "OK",
            () => Array.Empty<CashMovementReasonDto>()
        );

    [HttpPost]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCashMovementReasonCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCashMovementReasonCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return BadRequest();
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ToggleCashMovementReasonCommand(id), ct));
}
