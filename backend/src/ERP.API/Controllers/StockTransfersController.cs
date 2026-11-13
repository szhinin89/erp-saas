using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.ConfirmStockTransfer;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockTransfer;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// ZH-API-THIN-STOCK-01 — transferencias entre bodegas (agregado StockTransfer: Draft → Confirmed),
// separadas de StockController sin cambiar contrato: misma ruta base, mismas policies (class
// [Authorize] + perm:inventory.stock.manage), mismo tag OpenAPI "Stock" y el mismo [AppFeature]
// "Transferencias entre bodegas" a nivel de acción. Comentario no-XML a propósito (ver
// StockAdjustmentsController).
[ApiController]
[Route("api/v1/inventory/stock")]
[Authorize]
[Tags("Stock")]
[Produces("application/json")]
public sealed class StockTransfersController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockTransfersController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Crea una transferencia entre bodegas en estado Draft.
    /// </summary>
    /// <remarks>
    /// Responde 201. El origen debe ser distinto del destino, con al menos una línea y solo productos de la
    /// empresa.
    /// </remarks>
    /// <param name="command">Bodega de origen, bodega de destino y líneas.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Transferencia creada.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [AppFeature(
        "Transferencias entre bodegas",
        $"perm:{InventoryPermissions.StockManage}",
        "swap_horiz",
        "/inventory/transfers",
        null,
        22
    )]
    [HttpPost("transfers")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<StockTransferDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateStockTransferCommand command,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>
    /// Confirma una transferencia Draft.
    /// </summary>
    /// <remarks>
    /// Mueve el stock entre bodegas: registra la salida en el origen al costo promedio corrido y la entrada en el
    /// destino. No existe operación de anulación de transferencias.
    /// </remarks>
    /// <param name="id">Id de la transferencia.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Transferencia confirmada.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("transfers/{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockManage}")]
    [ProducesResponseType(typeof(ApiResponse<StockTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmTransfer(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ConfirmStockTransferCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }
}
