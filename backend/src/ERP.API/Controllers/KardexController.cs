using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexByDocument;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexByProduct;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexMovementDetail;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Expediente del Kardex: consulta de trazabilidad histórica de inventario.
/// StockMovement es la fuente de verdad; el detalle de un movimiento se compone
/// (sin duplicar datos) con el documento origen del módulo dueño (Purchases/Sales).
/// </summary>
[AppFeature("Kardex", $"perm:{InventoryPermissions.StockView}", "history", "/inventory/kardex", null, 21)]
[ApiController]
[Route("api/v1/inventory/kardex")]
[Authorize]
[Produces("application/json")]
public sealed class KardexController : ControllerBase
{
    private readonly IMediator _mediator;

    public KardexController(IMediator mediator) => _mediator = mediator;

    /// <summary>Historial del Kardex de un producto (todas las bodegas, o una si se filtra).</summary>
    [HttpGet("{productId:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<IReadOnlyList<StockMovementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKardexByProduct(
        Guid productId, [FromQuery] Guid? warehouseId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetKardexByProductQuery(productId, warehouseId, from, to), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<StockMovementDto>());
    }

    /// <summary>Movimientos generados por un documento origen específico (búsqueda exacta por Id).</summary>
    [HttpGet("by-document/{sourceDocId:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<IReadOnlyList<StockMovementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKardexByDocument(
        Guid sourceDocId, [FromQuery] string sourceDocType,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetKardexByDocumentQuery(sourceDocId, sourceDocType), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<StockMovementDto>());
    }

    /// <summary>Expediente completo de un movimiento: hecho de inventario + documento origen + actor.</summary>
    [HttpGet("movement/{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<KardexMovementDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovementDetail(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetKardexMovementDetailQuery(id), ct);
        return this.ToOkOrBadRequest(result);
    }
}
