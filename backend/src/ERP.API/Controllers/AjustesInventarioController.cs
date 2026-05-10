using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Inventario.DTOs;
using ERP.Application.Inventario.UseCases.CancelarAjuste;
using ERP.Application.Inventario.UseCases.CrearAjuste;
using ERP.Application.Inventario.UseCases.EjecutarAjuste;
using ERP.Application.Inventario.UseCases.GetAjusteById;
using ERP.Application.Inventario.UseCases.GetAjustesList;

namespace ERP.API.Controllers;

/// <summary>
/// Ajustes manuales de stock (incremento o disminución) sobre un producto en una bodega.
/// Flujo: Borrador → Ejecutado (stock afectado) | Cancelado (sin efecto).
/// </summary>
[ApiController]
[Route("api/inventario/ajustes")]
[Authorize]
[Produces("application/json")]
public sealed class AjustesInventarioController : ControllerBase
{
    private readonly IMediator _mediator;

    public AjustesInventarioController(IMediator mediator) => _mediator = mediator;

    // ── Queries ───────────────────────────────────────────────────────────

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
    [ProducesResponseType(typeof(ApiResponse<AjusteInventarioDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAjusteByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── Crear ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea un ajuste en estado Borrador. El stock NO se modifica hasta Ejecutar.
    /// </summary>
    /// <response code="201">Ajuste creado en estado Borrador.</response>
    /// <response code="400">Bodega/producto inválidos, cantidad cero o motivo vacío.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.ajustes.create")]
    [ProducesResponseType(typeof(ApiResponse<AjusteInventarioDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearAjusteCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // ── Transiciones de estado ────────────────────────────────────────────

    /// <summary>
    /// Ejecuta el ajuste: actualiza el stock inmediatamente (atómico).
    /// Para disminuciones, falla si el stock disponible es insuficiente.
    /// </summary>
    /// <response code="200">Stock actualizado y ajuste en estado Ejecutado.</response>
    /// <response code="400">Stock insuficiente o estado no válido.</response>
    [HttpPatch("{id:guid}/ejecutar")]
    [Authorize(Policy = "perm:inventario.ajustes.execute")]
    [ProducesResponseType(typeof(ApiResponse<AjusteInventarioDto>), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(ApiResponse<AjusteInventarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelarAjusteCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Cancelado");
    }
}
