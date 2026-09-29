using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetAggregatedStock;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetCurrentStockReport;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetItemWarehouseAvailability;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetStock;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// Consultas de existencias (saldo actual, reporte, movimientos, agregado por item, disponibilidad
// por bodega). Sin [AppFeature]: "Stock" es un concepto interno, no una funcionalidad visible en el
// menú; el acceso operativo del usuario es "Inventario → Kardex" (ver KardexController).
// ZH-API-THIN-STOCK-01: ajustes y transferencias viven en StockAdjustmentsController y
// StockTransfersController, bajo la misma ruta base y el mismo tag OpenAPI "Stock".
[ApiController]
[Route("api/v1/inventory/stock")]
[Authorize]
[Tags("Stock")]
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

    /// <summary>
    /// Reporte básico de stock actual por bodega (piloto Sumak). Sin warehouseId, incluye
    /// todas las bodegas de la empresa activa.
    /// </summary>
    [HttpGet("report")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<StockReportRowDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetReport(
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? search,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(new GetCurrentStockReportQuery(warehouseId, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<StockReportRowDto>());
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
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
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
}
