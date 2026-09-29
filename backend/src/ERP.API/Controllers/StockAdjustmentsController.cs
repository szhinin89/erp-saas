using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.CancelStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.ListStockAdjustments;
using ERP.Application.Modules.Inventory.Stock.UseCases.UpdateStockAdjustment;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// ZH-API-THIN-STOCK-01 — ajustes de inventario (agregado StockAdjustment: Draft → Executed →
// Cancelled), separados de StockController sin cambiar contrato: misma ruta base, mismas policies
// (class [Authorize] + perm:inventory.adjustments.*), mismo tag OpenAPI "Stock" y el mismo
// [AppFeature] "Ajustes de inventario" a nivel de acción. Comentario no-XML a propósito: un
// <summary> de clase agregaría un tag nuevo al documento OpenAPI.
[ApiController]
[Route("api/v1/inventory/stock")]
[Authorize]
[Tags("Stock")]
[Produces("application/json")]
public sealed class StockAdjustmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockAdjustmentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista ajustes de inventario paginados, con filtros.</summary>
    [AppFeature(
        "Ajustes de inventario",
        $"perm:{InventoryPermissions.AdjustmentsView}",
        "tune",
        "/inventory/adjustments",
        null,
        23
    )]
    [HttpGet("adjustments")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PagedResult<StockAdjustmentDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> ListAdjustments(
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? status,
        [FromQuery] Guid? reasonId,
        [FromQuery] string? movementType,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new ListStockAdjustmentsQuery(
                warehouseId,
                status,
                reasonId,
                movementType,
                startDate,
                endDate,
                pageNumber,
                pageSize
            ),
            ct
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Obtiene un ajuste de inventario por Id (cabecera + líneas).</summary>
    [HttpGet("adjustments/{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsView}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<StockAdjustmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdjustment(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetStockAdjustmentByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Crea un ajuste de inventario en estado Draft.</summary>
    [HttpPost("adjustments")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsCreate}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<StockAdjustmentDto>),
        StatusCodes.Status201Created
    )]
    public async Task<IActionResult> CreateAdjustment(
        [FromBody] CreateStockAdjustmentCommand command,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Actualiza un ajuste en Draft (cabecera + reemplazo total de líneas).</summary>
    [HttpPut("adjustments/{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsUpdate}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<StockAdjustmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAdjustment(
        Guid id,
        [FromBody] UpdateStockAdjustmentCommand command,
        CancellationToken ct = default
    )
    {
        if (id != command.Id)
            return BadRequest();
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Ejecuta un ajuste Draft: aplica el/los movimiento(s) de stock.</summary>
    [HttpPost("adjustments/{id:guid}/execute")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsConfirm}")]
    public async Task<IActionResult> ExecuteAdjustment(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ExecuteStockAdjustmentCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Anula un ajuste Ejecutado posteando movimientos inversos de Kardex.</summary>
    [HttpPost("adjustments/{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsCancel}")]
    public async Task<IActionResult> CancelAdjustment(
        Guid id,
        [FromBody] CancelStockAdjustmentRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new CancelStockAdjustmentCommand(id, request.Reason),
            ct
        );
        return this.ToOkOrBadRequest(result);
    }
}

public sealed record CancelStockAdjustmentRequest(string Reason);
