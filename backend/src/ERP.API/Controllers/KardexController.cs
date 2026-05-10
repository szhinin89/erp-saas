using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ERP.API.Contracts;
using ERP.API.Export;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventario.DTOs;
using ERP.Application.Inventario.UseCases.GetKardex;
using ERP.Application.Inventario.UseCases.RecalcularSnapshots;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;
using ERP.Infrastructure.BackgroundServices;

namespace ERP.API.Controllers;

/// <summary>
/// Kardex valorizado de inventario por producto y bodega (método: promedio ponderado móvil).
/// Soporta modo síncrono (respuesta inmediata) y asíncrono (202 + jobId).
/// </summary>
[ApiController]
[Route("api/inventario/kardex")]
[Authorize]
[Produces("application/json")]
public sealed class KardexController : ControllerBase
{
    private readonly IMediator              _mediator;
    private readonly KardexOptions          _opts;
    private readonly ICurrentTenant         _tenant;
    private readonly IKardexReporteRepository _reporteRepo;
    private readonly KardexReporteQueue     _queue;

    public KardexController(
        IMediator                  mediator,
        IOptions<KardexOptions>    opts,
        ICurrentTenant             tenant,
        IKardexReporteRepository   reporteRepo,
        KardexReporteQueue         queue)
    {
        _mediator    = mediator;
        _opts        = opts.Value;
        _tenant      = tenant;
        _reporteRepo = reporteRepo;
        _queue       = queue;
    }

    // ── Kardex síncrono / redirección automática a async ─────────────────────

    /// <summary>
    /// Retorna el kardex valorizado de un producto en una bodega.
    /// Si el rango supera <c>MaxDaysForSync</c> y el modo asíncrono está activo,
    /// retorna 202 Accepted con un <c>jobId</c> para consultar el resultado más tarde.
    /// </summary>
    /// <remarks>
    /// Query params: productoId, bodegaId, fechaInicio (YYYY-MM-DD), fechaFin (YYYY-MM-DD).
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetKardex(CancellationToken ct = default)
    {
        var (ok, productoId, bodegaId, fechaInicio, fechaFin, err) = ParseQueryParams();
        if (!ok) return err!;

        // Redirección automática a async si el rango es muy largo
        if (DebeUsarAsync(fechaInicio, fechaFin))
            return await EnqueueReporteAsync(productoId, bodegaId, fechaInicio, fechaFin, ct);

        var result = await _mediator.Send(
            new GetKardexQuery(productoId, bodegaId, fechaInicio, fechaFin), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── Solicitar reporte asíncrono explícitamente ────────────────────────────

    /// <summary>
    /// Solicita el kardex en modo asíncrono.
    /// Siempre retorna 202 Accepted con un <c>jobId</c>; el resultado se consulta en
    /// <c>GET /api/inventario/kardex/resultado/{jobId}</c>.
    /// </summary>
    [HttpPost("solicitar")]
    [Authorize(Policy = "perm:inventario.kardex.view")]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SolicitarKardex(CancellationToken ct = default)
    {
        var (ok, productoId, bodegaId, fechaInicio, fechaFin, err) = ParseQueryParams();
        if (!ok) return err!;

        return await EnqueueReporteAsync(productoId, bodegaId, fechaInicio, fechaFin, ct);
    }

    // ── Obtener resultado de un reporte asíncrono ─────────────────────────────

    /// <summary>
    /// Retorna el resultado de un reporte de Kardex generado de forma asíncrona.
    /// </summary>
    /// <param name="jobId">ID del reporte obtenido en la solicitud.</param>
    /// <response code="200">Reporte completado.</response>
    /// <response code="202">Reporte aún en proceso.</response>
    /// <response code="404">Reporte no encontrado.</response>
    /// <response code="400">El reporte terminó con error.</response>
    [HttpGet("resultado/{jobId:guid}")]
    [Authorize(Policy = "perm:inventario.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObtenerResultado(Guid jobId, CancellationToken ct = default)
    {
        var reporte = await _reporteRepo.GetByIdAsync(_tenant.TenantId, jobId, ct);

        if (reporte is null)
            return NotFound(new { mensaje = "Reporte no encontrado." });

        if (reporte.Estado == KardexReporte.EstadoPendiente ||
            reporte.Estado == KardexReporte.EstadoProcesando)
        {
            Response.Headers.Append("Retry-After", "5");
            return Accepted(new
            {
                jobId   = reporte.Id,
                estado  = reporte.Estado,
                mensaje = "El reporte aún está siendo procesado. Consulte nuevamente en unos segundos.",
            });
        }

        if (reporte.Estado == KardexReporte.EstadoError)
            return BadRequest(new { mensaje = reporte.ErrorMensaje });

        // Completado: deserializar y retornar
        var kardex = reporte.ResultadoJson is not null
            ? JsonSerializer.Deserialize<KardexResponse>(reporte.ResultadoJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : null;

        return kardex is null
            ? BadRequest(new { mensaje = "El resultado del reporte no pudo deserializarse." })
            : Ok(new { data = kardex, message = "OK" });
    }

    // ── Exportar Excel ────────────────────────────────────────────────────────

    /// <summary>Exporta el kardex a Excel (.xlsx) con tabla formateada y resumen.</summary>
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

        if (!result.IsSuccess) return BadRequest(result.Error);

        var bytes    = KardexExcelExporter.Generate(result.Value!, fechaInicio, fechaFin);
        var fileName = BuildFileName(result.Value!, fechaInicio, fechaFin, "xlsx");

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ── Exportar PDF ──────────────────────────────────────────────────────────

    /// <summary>Exporta el kardex a PDF (A4 horizontal) con tabla y resumen.</summary>
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

        if (!result.IsSuccess) return BadRequest(result.Error);

        var bytes    = KardexPdfExporter.Generate(result.Value!, fechaInicio, fechaFin);
        var fileName = BuildFileName(result.Value!, fechaInicio, fechaFin, "pdf");

        return File(bytes, "application/pdf", fileName);
    }

    // ── Recalcular snapshots (administrador) ──────────────────────────────────

    /// <summary>
    /// Recalcula los snapshots diarios del kardex bajo demanda.
    /// Útil para reconstruir el historial tras migración o correcciones manuales.
    /// Requiere rol SuperAdmin o Administrador.
    /// </summary>
    [HttpPost("recalcular")]
    [Authorize(Policy = "perm:inventario.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecalcularSnapshots(
        [FromBody] RecalcularSnapshotsBody body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new RecalcularSnapshotsCommand(body.ProductoId, body.BodegaId, body.Hasta), ct);

        if (!result.IsSuccess)
            return BadRequest(new { mensaje = result.Error });
        return Ok(new { data = new { snapshotsGenerados = result.Value }, message = "OK" });
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

    private bool DebeUsarAsync(DateTime? fechaInicio, DateTime? fechaFin)
    {
        if (!_opts.UseScalableMode || !_opts.EnableAsyncReport) return false;
        if (fechaInicio is null || fechaFin is null) return false;

        var dias = (fechaFin.Value.Date - fechaInicio.Value.Date).Days;
        return dias > _opts.MaxDaysForSync;
    }

    private async Task<IActionResult> EnqueueReporteAsync(
        Guid productoId, Guid bodegaId,
        DateTime? fechaInicio, DateTime? fechaFin,
        CancellationToken ct)
    {
        var reporte = KardexReporte.Create(
            _tenant.TenantId, productoId, bodegaId, fechaInicio, fechaFin);

        await _reporteRepo.AddAsync(reporte, ct);
        await _reporteRepo.SaveChangesAsync(ct);

        await _queue.Writer.WriteAsync(reporte.Id, ct);

        Response.Headers.Append("Retry-After", "10");
        return Accepted(new
        {
            jobId   = reporte.Id,
            estado  = reporte.Estado,
            mensaje = "El reporte está siendo procesado. " +
                      $"Consulte el resultado en /api/inventario/kardex/resultado/{reporte.Id}",
        });
    }

    private static string BuildFileName(
        KardexResponse k, DateTime? desde, DateTime? hasta, string ext)
    {
        var code    = k.Producto.Codigo.Replace("/", "-");
        var periodo = desde.HasValue || hasta.HasValue
            ? $"_{desde?.ToString("yyyyMMdd") ?? "inicio"}-{hasta?.ToString("yyyyMMdd") ?? "hoy"}"
            : "";
        return $"Kardex_{code}{periodo}.{ext}";
    }
}

/// <summary>Body del endpoint <c>POST /recalcular</c>.</summary>
public sealed record RecalcularSnapshotsBody(
    Guid?     ProductoId = null,
    Guid?     BodegaId   = null,
    DateTime? Hasta      = null);
