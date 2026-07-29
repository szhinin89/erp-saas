using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.ConfirmStockTransfer;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockTransfer;
using ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetAggregatedStock;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetItemWarehouseAvailability;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetStock;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// Sin [AppFeature]: "Stock" es un concepto interno (ajustes/transferencias/consulta de saldo
// actual), no una funcionalidad visible en el menú. El acceso operativo del usuario es
// "Inventario → Kardex" (ver KardexController), que compone el historial completo.
[ApiController]
[Route("api/v1/inventory/stock")]
[Authorize]
[Produces("application/json")]
public sealed class StockController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockController(IMediator mediator) => _mediator = mediator;

    /// <summary>Consulta stock actual por item y/o bodega.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<CurrentStockDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetStock(
        [FromQuery] Guid? itemId,
        [FromQuery] Guid? warehouseId,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(new GetStockQuery(itemId, warehouseId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<CurrentStockDto>());
    }

    /// <summary>Consulta movimientos de stock por item y bodega.</summary>
    [HttpGet("movements")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<StockMovementDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetMovements(
        [FromQuery] Guid itemId,
        [FromQuery] Guid warehouseId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new GetStockMovementsQuery(itemId, warehouseId, from, to),
            ct
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<StockMovementDto>());
    }

    /// <summary>Stock agregado de un item a través de todas las bodegas.</summary>
    [HttpGet("aggregated/{itemId:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<AggregatedStockDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAggregated(Guid itemId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAggregatedStockQuery(itemId), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Disponibilidad de un item por bodega (para selectores de bodega en documentos de venta).</summary>
    [HttpGet("items/{itemId:guid}/warehouse-availability")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<ItemWarehouseAvailabilityDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetWarehouseAvailability(
        Guid itemId,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(new GetItemWarehouseAvailabilityQuery(itemId), ct);
        return this.ToOkOrBadRequest(
            result,
            "OK",
            () => Array.Empty<ItemWarehouseAvailabilityDto>()
        );
    }

    /// <summary>Crea un ajuste de inventario en estado Draft.</summary>
    [HttpPost("adjustments")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockManage}")]
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

    /// <summary>Ejecuta un ajuste Draft: aplica el movimiento de stock.</summary>
    [HttpPost("adjustments/{id:guid}/execute")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockManage}")]
    public async Task<IActionResult> ExecuteAdjustment(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ExecuteStockAdjustmentCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Crea una transferencia entre bodegas en estado Draft.</summary>
    [HttpPost("transfers")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<StockTransferDto>),
        StatusCodes.Status201Created
    )]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateStockTransferCommand command,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Confirma una transferencia Draft: mueve stock entre bodegas.</summary>
    [HttpPost("transfers/{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockManage}")]
    public async Task<IActionResult> ConfirmTransfer(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ConfirmStockTransferCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }
}
