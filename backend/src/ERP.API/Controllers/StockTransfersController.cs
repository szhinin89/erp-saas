using ERP.API.Attributes;
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
    /// Crea una transferencia entre bodegas en estado Draft. Único punto de entrada de menú de
    /// "Stock" (P1-INVENTORY-WAREHOUSE-TRANSFER-UI-01): la clase no tiene [AppFeature] (ver
    /// comentario de cabecera — Stock en general no es menú-visible), pero Transferencias sí es
    /// una pantalla propia — [AppFeature] a nivel de método (soportado por el atributo) le da
    /// entrada de menú dedicada sin exponer Ajustes/consultas de Stock como ítems de menú.
    /// Reutiliza el mismo permiso que ya protege este endpoint — no crea uno nuevo.
    /// </summary>
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
