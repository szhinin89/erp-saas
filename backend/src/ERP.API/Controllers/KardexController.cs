using ERP.API.Attributes;
using ERP.API.Contracts;
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
[AppFeature(
    "Kardex",
    $"perm:{InventoryPermissions.StockView}",
    "history",
    "/inventory/kardex",
    null,
    21
)]
[ApiController]
[Route("api/v1/inventory/kardex")]
[Authorize]
[Produces("application/json")]
public sealed class KardexController : ControllerBase
{
    private readonly IMediator _mediator;

    public KardexController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Historial de Kardex de un producto.
    /// </summary>
    /// <remarks>
    /// Incluye todas las bodegas o solo la indicada. Si no hay resultados devuelve una lista vacía.
    /// </remarks>
    /// <param name="productId">Id del producto (ítem).</param>
    /// <param name="warehouseId">Filtra por bodega; omitido, incluye todas.</param>
    /// <param name="from">Fecha de negocio inicial (<c>YYYY-MM-DD</c>).</param>
    /// <param name="to">Fecha de negocio final (<c>YYYY-MM-DD</c>).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Movimientos del Kardex.</response>
    [HttpGet("{productId:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<StockMovementDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetKardexByProduct(
        Guid productId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new GetKardexByProductQuery(productId, warehouseId, from, to),
            ct
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<StockMovementDto>());
    }

    /// <summary>
    /// Movimientos de Kardex generados por un documento origen.
    /// </summary>
    /// <remarks>
    /// Búsqueda exacta por id y tipo del documento origen. Si no hay resultados devuelve una lista vacía.
    /// </remarks>
    /// <param name="sourceDocId">Id del documento origen.</param>
    /// <param name="sourceDocType">Tipo del documento origen.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Movimientos del documento.</response>
    [HttpGet("by-document/{sourceDocId:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<StockMovementDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetKardexByDocument(
        Guid sourceDocId,
        [FromQuery] string sourceDocType,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new GetKardexByDocumentQuery(sourceDocId, sourceDocType),
            ct
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<StockMovementDto>());
    }

    /// <summary>
    /// Detalle completo de un movimiento de Kardex.
    /// </summary>
    /// <remarks>
    /// Reúne el hecho de inventario, el documento origen y el actor.
    /// </remarks>
    /// <param name="id">Id de el recurso.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Detalle del movimiento.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("movement/{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.StockView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<KardexMovementDetailDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMovementDetail(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetKardexMovementDetailQuery(id), ct);
        return this.ToOkOrBadRequest(result);
    }
}
