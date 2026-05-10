using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Export;
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

    // ── Query JSON ────────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna el kardex valorizado de un producto en una bodega.
    /// Incluye saldo inicial, movimientos del período, costo unitario promedio ponderado y resumen.
    /// </summary>
    /// <remarks>
    /// Query params:
    /// - productoId  (Guid, requerido)
    /// - bodegaId    (Guid, requerido)
    /// - fechaInicio (YYYY-MM-DD, opcional) — primer día del período
    /// - fechaFin    (YYYY-MM-DD, opcional) — último día del período (inclusivo)
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetKardex(CancellationToken ct = default)
    {
        var (ok, productoId, bodegaId, fechaInicio, fechaFin, err) = ParseQueryParams();
        if (!ok) return err!;

        var result = await _mediator.Send(
            new GetKardexQuery(productoId, bodegaId, fechaInicio, fechaFin), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── Exportar Excel ────────────────────────────────────────────────────────

    /// <summary>
    /// Exporta el kardex a Excel (.xlsx) con tabla formateada y resumen.
    /// </summary>
    /// <remarks>
    /// Mismos query params que GET /api/inventario/kardex.
    /// Retorna el archivo directamente para descarga.
    /// </remarks>
    [HttpGet("exportar/excel")]
    [Authorize(Policy = "perm:inventario.kardex.view")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportarExcel(CancellationToken ct = default)
    {
        var (ok, productoId, bodegaId, fechaInicio, fechaFin, err) = ParseQueryParams();
        if (!ok) return err!;

        var result = await _mediator.Send(
            new GetKardexQuery(productoId, bodegaId, fechaInicio, fechaFin), ct);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var bytes    = KardexExcelExporter.Generate(result.Value!, fechaInicio, fechaFin);
        var fileName = BuildFileName(result.Value!, fechaInicio, fechaFin, "xlsx");

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ── Exportar PDF ──────────────────────────────────────────────────────────

    /// <summary>
    /// Exporta el kardex a PDF (A4 horizontal) con tabla completa y resumen.
    /// </summary>
    /// <remarks>
    /// Mismos query params que GET /api/inventario/kardex.
    /// Retorna el archivo directamente para descarga.
    /// </remarks>
    [HttpGet("exportar/pdf")]
    [Authorize(Policy = "perm:inventario.kardex.view")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportarPdf(CancellationToken ct = default)
    {
        var (ok, productoId, bodegaId, fechaInicio, fechaFin, err) = ParseQueryParams();
        if (!ok) return err!;

        var result = await _mediator.Send(
            new GetKardexQuery(productoId, bodegaId, fechaInicio, fechaFin), ct);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var bytes    = KardexPdfExporter.Generate(result.Value!, fechaInicio, fechaFin);
        var fileName = BuildFileName(result.Value!, fechaInicio, fechaFin, "pdf");

        return File(bytes, "application/pdf", fileName);
    }

    // ── Helpers privados ──────────────────────────────────────────────────────

    private (bool Ok, Guid ProductoId, Guid BodegaId,
             DateTime? FechaInicio, DateTime? FechaFin,
             IActionResult? Error)
        ParseQueryParams()
    {
        if (!Request.Query.TryGetValue("productoId", out var pv) || !Guid.TryParse(pv, out var productoId))
            return (false, default, default, null, null,
                BadRequest("productoId es requerido y debe ser un GUID válido."));

        if (!Request.Query.TryGetValue("bodegaId", out var bv) || !Guid.TryParse(bv, out var bodegaId))
            return (false, default, default, null, null,
                BadRequest("bodegaId es requerido y debe ser un GUID válido."));

        DateTime? fechaInicio = null, fechaFin = null;
        if (Request.Query.TryGetValue("fechaInicio", out var fiv) && DateTime.TryParse(fiv, out var fi))
            fechaInicio = fi;
        if (Request.Query.TryGetValue("fechaFin",    out var ffv) && DateTime.TryParse(ffv, out var ff))
            fechaFin = ff;

        return (true, productoId, bodegaId, fechaInicio, fechaFin, null);
    }

    private static string BuildFileName(
        KardexResponse k, DateTime? desde, DateTime? hasta, string ext)
    {
        var code   = k.Producto.Codigo.Replace("/", "-");
        var periodo = desde.HasValue || hasta.HasValue
            ? $"_{desde?.ToString("yyyyMMdd") ?? "inicio"}-{hasta?.ToString("yyyyMMdd") ?? "hoy"}"
            : "";
        return $"Kardex_{code}{periodo}.{ext}";
    }
}
