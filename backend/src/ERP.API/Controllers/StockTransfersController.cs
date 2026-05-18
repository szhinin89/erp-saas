using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.CancelarTransferencia;
using ERP.Application.Inventory.UseCases.ConfirmarTransferencia;
using ERP.Application.Inventory.UseCases.CrearTransferencia;
using ERP.Application.Inventory.UseCases.GetTransferenciaById;
using ERP.Application.Inventory.UseCases.GetTransferenciasList;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Transferencias de stock entre bodegas del mismo tenant.
/// Flujo: Borrador → Confirmado (stock movido) | Cancelado (sin efecto en stock).
/// </summary>
[AppFeature("Transferencias", "perm:inventory.transfers.view", "↔️", "/inventory/transfers", "perm:inventory.products.view", 44)]
[ApiController]
[Route("api/inventory/transfers")]
[Authorize]
[Produces("application/json")]
public sealed class StockTransfersController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockTransfersController(IMediator mediator) => _mediator = mediator;

    // ── Queries ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Lista paginada de transferencias con filtros opcionales.</summary>
    /// <remarks>Query params: pageNumber, pageSize, sourceWarehouseId, destinationWarehouseId, estado, fechaDesde (YYYY-MM-DD), fechaHasta.</remarks>
    [HttpGet]
    [Authorize(Policy = "perm:inventory.transfers.view")]
    [ProducesResponseType(typeof(ApiResponse<TransfersPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        int pageNumber = 1, pageSize = 20;
        if (Request.Query.TryGetValue("pageNumber", out var pnv) && int.TryParse(pnv, out var pni)) pageNumber = pni;
        if (Request.Query.TryGetValue("pageSize",   out var psv) && int.TryParse(psv, out var psi)) pageSize   = psi;

        Guid? sourceWarehouseId = null, destinationWarehouseId = null;
        if (Request.Query.TryGetValue("bodegaOrigenId",  out var bov) && Guid.TryParse(bov, out var boid)) sourceWarehouseId      = boid;
        if (Request.Query.TryGetValue("bodegaDestinoId", out var bdv) && Guid.TryParse(bdv, out var bdid)) destinationWarehouseId = bdid;

        var status = Request.Query.TryGetValue("estado", out var ev) ? ev.ToString() : null;

        DateTime? desde = null, hasta = null;
        if (Request.Query.TryGetValue("fechaDesde", out var fdv) && DateTime.TryParse(fdv, out var fd)) desde = fd;
        if (Request.Query.TryGetValue("fechaHasta", out var fhv) && DateTime.TryParse(fhv, out var fh)) hasta = fh;

        var result = await _mediator.Send(
            new GetTransfersListQuery(pageNumber, pageSize, sourceWarehouseId, destinationWarehouseId, status, desde, hasta), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Retorna el detalle completo de una transferencia (con ítems).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inventory.transfers.view")]
    [ProducesResponseType(typeof(ApiResponse<TransferDetailDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTransferByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── Crear ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea una transferencia en estado Borrador.
    /// El stock NO se mueve hasta Confirmar.
    /// </summary>
    /// <response code="201">Transferencia creada en estado Borrador.</response>
    /// <response code="400">Bodegas inválidas o stock insuficiente en la bodega origen.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventory.transfers.create")]
    [ProducesResponseType(typeof(ApiResponse<TransferDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTransferCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // ── Transiciones de estado ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Confirma la transferencia: mueve el stock de la bodega origen a la destino.
    /// Operación atómica — si falta stock en algún ítem, se revierte todo.
    /// </summary>
    /// <response code="200">Stock movido y transferencia en estado Confirmado.</response>
    /// <response code="400">Stock insuficiente o estado no válido.</response>
    [HttpPatch("{id:guid}/confirmar")]
    [Authorize(Policy = "perm:inventory.transfers.confirm")]
    [ProducesResponseType(typeof(ApiResponse<TransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ConfirmTransferCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Confirmado");
    }

    /// <summary>
    /// Cancela la transferencia. Solo posible en estado Borrador.
    /// No afecta el stock.
    /// </summary>
    [HttpPatch("{id:guid}/cancelar")]
    [Authorize(Policy = "perm:inventory.transfers.cancel")]
    [ProducesResponseType(typeof(ApiResponse<TransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelTransferCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Cancelado");
    }
}
