using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Application.Modules.Inventory.UseCases.GetCurrentStockByWarehouse;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Consultas de inventario (stock por bodega).</summary>
[AppFeature("Stock por bodega", "perm:inventory.stock.view", "📦", "/inventory/stock", "perm:inventory.products.view", 41)]
[ApiController]
[Route("api/inventory/stock")]
[Authorize]
[Produces("application/json")]
public sealed class StockController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockController(IMediator mediator) => _mediator = mediator;

    /// <summary>Saldo de stock en una bodega; filtro opcional por producto.</summary>
    [HttpGet("stock-actual")]
    [Authorize(Policy = "perm:inventory.stock.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CurrentStockListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCurrentStock(
        [FromQuery(Name = "bodegaId")] Guid warehouseId,
        [FromQuery(Name = "productoId")] Guid? productId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCurrentStockPorWarehouseQuery(warehouseId, productId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<CurrentStockListItemDto>());
    }
}
