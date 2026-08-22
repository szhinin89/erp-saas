using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.CreateInventoryAdjustmentReason;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ListInventoryAdjustmentReasons;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ToggleInventoryAdjustmentReason;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.UpdateInventoryAdjustmentReason;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo administrable de motivos de ajuste de inventario (INVENTORY-ADJUSTMENTS-02) — SSOT
/// dinámico consumido por CreateStockAdjustment/ExecuteStockAdjustment. No borra físico: Toggle
/// solo activa/desactiva (MasterEntity.Enable/Disable).
/// </summary>
[AppFeature(
    "Motivos de ajuste",
    $"perm:{InventoryPermissions.AdjustmentReasonsView}",
    "list_alt",
    "/inventory/adjustment-reasons",
    null,
    24
)]
[ApiController]
[Route("api/v1/inventory/adjustment-reasons")]
[Authorize]
[Produces("application/json")]
public sealed class InventoryAdjustmentReasonsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryAdjustmentReasonsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista motivos de ajuste de inventario.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<InventoryAdjustmentReasonDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new ListInventoryAdjustmentReasonsQuery(companyId, includeInactive),
            ct
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<InventoryAdjustmentReasonDto>());
    }

    /// <summary>Crea un motivo de ajuste de inventario.</summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<InventoryAdjustmentReasonDto>),
        StatusCodes.Status201Created
    )]
    public async Task<IActionResult> Create(
        [FromBody] CreateInventoryAdjustmentReasonCommand command,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Actualiza un motivo de ajuste de inventario (Code inmutable).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<InventoryAdjustmentReasonDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateInventoryAdjustmentReasonCommand command,
        CancellationToken ct = default
    )
    {
        if (id != command.Id)
            return BadRequest();
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Activa/desactiva un motivo (soft toggle — nunca borra físico).</summary>
    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<InventoryAdjustmentReasonDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Toggle(
        Guid id,
        [FromBody] ToggleInventoryAdjustmentReasonRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new ToggleInventoryAdjustmentReasonCommand(id, request.Activate),
            ct
        );
        return this.ToOkOrBadRequest(result);
    }
}

public sealed record ToggleInventoryAdjustmentReasonRequest(bool Activate);
