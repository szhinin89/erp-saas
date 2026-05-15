using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.CancelarAjuste;
using ERP.Application.Inventory.UseCases.CrearAjuste;
using ERP.Application.Inventory.UseCases.EjecutarAjuste;
using ERP.Application.Inventory.UseCases.GetAjusteById;
using ERP.Application.Inventory.UseCases.GetAjustesList;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Ajustes manuales de stock (incremento o disminuciÃ³n) sobre un producto en una bodega.
/// Flujo: Borrador â†’ Ejecutado (stock afectado) | Cancelado (sin efecto).
/// </summary>
[Modulo("Ajustes de inventario", "perm:inventario.ajustes.view", "âš–ï¸", "/inventario/ajustes", "perm:inventario.products.view", 43)]
[ApiController]
[Route("api/inventario/ajustes")]
[Authorize]
[Produces("application/json")]
public sealed class AjustesInventarioController : ControllerBase
{
    private readonly IMediator _mediator;

    public AjustesInventarioController(IMediator mediator) => _mediator = mediator;

    // â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Lista paginada de ajustes con filtros opcionales.</summary>
    /// <remarks>Query params: pageNumber, pageSize, bodegaId, productoId, estado, fechaDesde (YYYY-MM-DD), fechaHasta.</remarks>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.ajustes.view")]
    [ProducesResponseType(typeof(ApiResponse<AjustesPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        int pageNumber = 1, pageSize = 20;
        if (Request.Query.TryGetValue("pageNumber", out var pnv) && int.TryParse(pnv, out var pni)) pageNumber = pni;
        if (Request.Query.TryGetValue("pageSize",   out var psv) && int.TryParse(psv, out var psi)) pageSize   = psi;

        Guid? bodegaId = null, productoId = null;
        if (Request.Query.TryGetValue("bodegaId",   out var bv) && Guid.TryParse(bv, out var bid)) bodegaId   = bid;
        if (Request.Query.TryGetValue("productoId", out var pv) && Guid.TryParse(pv, out var pid)) productoId = pid;

        var estado = Request.Query.TryGetValue("estado", out var ev) ? ev.ToString() : null;

        DateTime? desde = null, hasta = null;
        if (Request.Query.TryGetValue("fechaDesde", out var fdv) && DateTime.TryParse(fdv, out var fd)) desde = fd;
        if (Request.Query.TryGetValue("fechaHasta", out var fhv) && DateTime.TryParse(fhv, out var fh)) hasta = fh;

        var result = await _mediator.Send(
            new GetAjustesListQuery(pageNumber, pageSize, bodegaId, productoId, estado, desde, hasta), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Retorna el detalle de un ajuste.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inventario.ajustes.view")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAjusteByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // â”€â”€ Crear â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Crea un ajuste en estado Borrador. El stock NO se modifica hasta Ejecutar.
    /// </summary>
    /// <response code="201">Ajuste creado en estado Borrador.</response>
    /// <response code="400">Bodega/producto invÃ¡lidos, cantidad cero o motivo vacÃ­o.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.ajustes.create")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearAjusteCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // â”€â”€ Transiciones de estado â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Ejecuta el ajuste: actualiza el stock inmediatamente (atÃ³mico).
    /// Para disminuciones, falla si el stock disponible es insuficiente.
    /// </summary>
    /// <response code="200">Stock actualizado y ajuste en estado Ejecutado.</response>
    /// <response code="400">Stock insuficiente o estado no vÃ¡lido.</response>
    [HttpPatch("{id:guid}/ejecutar")]
    [Authorize(Policy = "perm:inventario.ajustes.execute")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ejecutar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EjecutarAjusteCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Ejecutado");
    }

    /// <summary>
    /// Cancela el ajuste. Solo posible en estado Borrador.
    /// No afecta el stock.
    /// </summary>
    [HttpPatch("{id:guid}/cancelar")]
    [Authorize(Policy = "perm:inventario.ajustes.cancel")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelarAjusteCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Cancelado");
    }
}

