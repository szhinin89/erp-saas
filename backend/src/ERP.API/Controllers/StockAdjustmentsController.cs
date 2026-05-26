using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.CancelAdjustment;
using ERP.Application.Inventory.UseCases.CreateAdjustment;
using ERP.Application.Inventory.UseCases.ExecuteAdjustment;
using ERP.Application.Inventory.UseCases.GetAdjustmentById;
using ERP.Application.Inventory.UseCases.GetAdjustmentsList;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Ajustes manuales de stock (incremento o disminución) sobre un producto en una bodega.
/// Flujo: Borrador → Ejecutado (stock afectado) | Cancelado (sin efecto).
/// </summary>
[AppFeature("Ajustes de inventario", "perm:inventory.adjustments.view", "⚖️", "/inventory/adjustments", "perm:inventory.products.view", 43)]
[ApiController]
[Route("api/inventory/adjustments")]
[Authorize]
[Produces("application/json")]
public sealed class StockAdjustmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockAdjustmentsController(IMediator mediator) => _mediator = mediator;

    // ── Queries ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Lista paginada de ajustes con filtros opcionales.</summary>
    /// <remarks>Query params: pageNumber, pageSize, bodegaId, productoId, estado, fechaDesde (YYYY-MM-DD), fechaHasta.</remarks>
    [HttpGet]
    [Authorize(Policy = "perm:inventory.adjustments.view")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentsPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        int pageNumber = 1, pageSize = 20;
        if (Request.Query.TryGetValue("pageNumber", out var pnv) && int.TryParse(pnv, out var pni)) pageNumber = pni;
        if (Request.Query.TryGetValue("pageSize",   out var psv) && int.TryParse(psv, out var psi)) pageSize   = psi;

        Guid? warehouseId = null, productId = null;
        if (Request.Query.TryGetValue("bodegaId",   out var bv) && Guid.TryParse(bv, out var bid)) warehouseId = bid;
        if (Request.Query.TryGetValue("productoId", out var pv) && Guid.TryParse(pv, out var pid)) productId   = pid;

        var status = Request.Query.TryGetValue("estado", out var ev) ? ev.ToString() : null;

        DateTime? from = null, to = null;
        if (Request.Query.TryGetValue("fechaDesde", out var fdv) && DateTime.TryParse(fdv, out var fd)) from = fd;
        if (Request.Query.TryGetValue("fechaHasta", out var fhv) && DateTime.TryParse(fhv, out var fh)) to   = fh;

        var result = await _mediator.Send(
            new GetStockAdjustmentsListQuery(pageNumber, pageSize, warehouseId, productId, status, from, to), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Retorna el detalle de un ajuste.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inventory.adjustments.view")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetStockAdjustmentByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── Crear ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea un ajuste en estado Borrador. El stock NO se modifica hasta Ejecutar.
    /// </summary>
    /// <response code="201">Ajuste creado en estado Borrador.</response>
    /// <response code="400">Bodega/producto inválidos, cantidad cero o motivo vacío.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventory.adjustments.create")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStockAdjustmentCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // ── Transiciones de estado ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta el ajuste: actualiza el stock inmediatamente (atómico).
    /// Para disminuciones, falla si el stock disponible es insuficiente.
    /// </summary>
    /// <response code="200">Stock actualizado y ajuste en estado Ejecutado.</response>
    /// <response code="400">Stock insuficiente o estado no válido.</response>
    [HttpPatch("{id:guid}/ejecutar")]
    [Authorize(Policy = "perm:inventory.adjustments.execute")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Execute(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ExecuteStockAdjustmentCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Ejecutado");
    }

    /// <summary>
    /// Cancela el ajuste. Solo posible en estado Borrador.
    /// No afecta el stock.
    /// </summary>
    [HttpPatch("{id:guid}/cancelar")]
    [Authorize(Policy = "perm:inventory.adjustments.cancel")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelStockAdjustmentCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Cancelado");
    }
}
