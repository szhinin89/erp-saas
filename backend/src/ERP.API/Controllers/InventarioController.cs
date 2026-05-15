using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Application.Modules.Inventory.UseCases.GetStockActualPorBodega;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Consultas de inventario (stock por bodega).</summary>
[Modulo("Stock por bodega", "session:inventario.stock", "📦", "/inventario/stock", "perm:inventario.products.view", 41)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class InventarioController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventarioController(IMediator mediator) => _mediator = mediator;

    /// <summary>Saldo de stock en una bodega; filtro opcional por producto.</summary>
    [HttpGet("stock-actual")]
    [Authorize(Policy = "perm:inventario.bodegas.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StockActualListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStockActual(
        [FromQuery] Guid bodegaId,
        [FromQuery] Guid? productoId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetStockActualPorBodegaQuery(bodegaId, productoId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<StockActualListItemDto>());
    }
}
