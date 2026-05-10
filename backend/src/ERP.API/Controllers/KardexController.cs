using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Application.Inventario.UseCases.GetKardex;

namespace ERP.API.Controllers;

/// <summary>
/// Kardex valorizado de inventario por producto y bodega (método: promedio ponderado móvil).
/// </summary>
[ApiController]
[Route("api/inventario/kardex")]
[Authorize]
[Produces("application/json")]
public sealed class KardexController : ControllerBase
{
    private readonly IMediator _mediator;

    public KardexController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Retorna el kardex valorizado de un producto en una bodega.
    /// Incluye saldo inicial, movimientos del período con columnas entrada/salida/saldo,
    /// costo unitario promedio ponderado móvil y resumen de totales.
    /// </summary>
    /// <remarks>
    /// Query params:
    /// - productoId (Guid, requerido)
    /// - bodegaId   (Guid, requerido)
    /// - fechaInicio (YYYY-MM-DD, opcional) — primer día del período; si se omite se incluye desde el inicio del historial
    /// - fechaFin    (YYYY-MM-DD, opcional) — último día del período (inclusivo); si se omite se incluye hasta hoy
    /// </remarks>
    /// <response code="200">Kardex calculado correctamente.</response>
    /// <response code="400">Producto o bodega no encontrados, o parámetros inválidos.</response>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetKardex(CancellationToken ct = default)
    {
        if (!Request.Query.TryGetValue("productoId", out var pv) || !Guid.TryParse(pv, out var productoId))
            return this.ToOkOrBadRequest(
                Result<KardexResponse>.Failure("productoId es requerido y debe ser un GUID válido."),
                "OK");

        if (!Request.Query.TryGetValue("bodegaId", out var bv) || !Guid.TryParse(bv, out var bodegaId))
            return this.ToOkOrBadRequest(
                Result<KardexResponse>.Failure("bodegaId es requerido y debe ser un GUID válido."),
                "OK");

        DateTime? fechaInicio = null, fechaFin = null;
        if (Request.Query.TryGetValue("fechaInicio", out var fiv) && DateTime.TryParse(fiv, out var fi))
            fechaInicio = fi;
        if (Request.Query.TryGetValue("fechaFin",    out var ffv) && DateTime.TryParse(ffv, out var ff))
            fechaFin = ff;

        var result = await _mediator.Send(
            new GetKardexQuery(productoId, bodegaId, fechaInicio, fechaFin), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }
}
