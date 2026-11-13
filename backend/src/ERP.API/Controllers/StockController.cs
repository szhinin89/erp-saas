using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.Costing;
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

    /// <summary>
    /// Lista los costos de inventario pendientes.
    /// </summary>
    /// <remarks>
    /// Costos de inventario que todavía no están determinados: sin base de costo conocida el costo queda nulo,
    /// nunca cero.
    /// </remarks>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Costos pendientes.</response>
    [HttpGet("costs/pending")]
    [Authorize(Policy = $"perm:{InventoryPermissions.PendingCostsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PendingInventoryCostDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingCosts(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ERP.Application.Modules.Inventory.Costing.GetPendingInventoryCostsQuery(), ct));

    /// <summary>
    /// Consulta el stock actual por ítem y bodega.
    /// </summary>
    /// <remarks>
    /// Ambos filtros son opcionales y se combinan. Si no hay resultados devuelve una lista vacía.
    /// </remarks>
    /// <param name="itemId">Filtra por ítem.</param>
    /// <param name="warehouseId">Filtra por bodega.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Stock actual.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<CurrentStockDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
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
    /// Reporte de stock actual por bodega.
    /// </summary>
    /// <remarks>
    /// Sin <c>warehouseId</c> incluye todas las bodegas de la empresa activa. Si no hay resultados devuelve una
    /// lista vacía.
    /// </remarks>
    /// <param name="warehouseId">Filtra por bodega; omitido, incluye todas.</param>
    /// <param name="search">Texto libre de búsqueda de ítems.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Filas del reporte de stock.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("report")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<StockReportRowDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? search,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(new GetCurrentStockReportQuery(warehouseId, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<StockReportRowDto>());
    }

    /// <summary>
    /// Consulta los movimientos de stock de un ítem en una bodega.
    /// </summary>
    /// <remarks>
    /// Si no hay resultados devuelve una lista vacía.
    /// </remarks>
    /// <param name="itemId">Id del ítem.</param>
    /// <param name="warehouseId">Id de la bodega.</param>
    /// <param name="from">Fecha de negocio inicial (<c>YYYY-MM-DD</c>); omitida, sin límite inferior.</param>
    /// <param name="to">Fecha de negocio final (<c>YYYY-MM-DD</c>); omitida, sin límite superior.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Movimientos de stock.</response>
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

    /// <summary>
    /// Stock agregado de un ítem en todas las bodegas.
    /// </summary>
    /// <param name="itemId">Id del ítem.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Stock agregado del ítem.</response>
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

    /// <summary>
    /// Disponibilidad de un ítem por bodega.
    /// </summary>
    /// <remarks>
    /// Pensado para los selectores de bodega de los documentos de venta. Si no hay resultados devuelve una lista
    /// vacía.
    /// </remarks>
    /// <param name="itemId">Id del ítem.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Disponibilidad por bodega.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("items/{itemId:guid}/warehouse-availability")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<ItemWarehouseAvailabilityDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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
