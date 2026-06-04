using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.RegisterLot;
using ERP.Application.Items.UseCases.GetLots;
using ERP.Application.Items.UseCases.RegisterSerial;
using ERP.Application.Items.UseCases.GetSerials;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.API.Controllers;

[AppFeature("Lotes y Series", "perm:inventory.lots.manage", "🔢", "/inventory/lots", null, 55)]
[ApiController]
[Route("api/inventory")]
[Authorize]
[Produces("application/json")]
public sealed class InventoryItemsController : ControllerBase
{
    private readonly IMediator _mediator;
    public InventoryItemsController(IMediator mediator) => _mediator = mediator;

    // ── Lots ──────────────────────────────────────────────────────────────

    [HttpGet("lots")]
    [Authorize(Policy = "perm:inventory.lots.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLots(
        [FromQuery] Guid       itemId,
        [FromQuery] Guid?      warehouseId = null,
        [FromQuery] LotStatus? status      = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetLotsQuery(itemId, warehouseId, status), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost("lots")]
    [Authorize(Policy = "perm:inventory.lots.manage")]
    [ProducesResponseType(typeof(ApiResponse<LotDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterLot(
        [FromBody] RegisterLotCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Lote registrado");
    }

    // ── Serials ───────────────────────────────────────────────────────────

    [HttpGet("serials")]
    [Authorize(Policy = "perm:inventory.serials.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SerialDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSerials(
        [FromQuery] Guid          itemId,
        [FromQuery] Guid?         warehouseId = null,
        [FromQuery] SerialStatus? status      = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSerialsQuery(itemId, warehouseId, status), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost("serials")]
    [Authorize(Policy = "perm:inventory.serials.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SerialDto>>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterSerials(
        [FromBody] RegisterSerialCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Seriales registrados");
    }
}
