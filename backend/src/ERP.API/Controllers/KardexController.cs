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
using ERP.Application.Inventory.DTOs;
using ERP.API.Attributes;
using ERP.Application.Inventory.UseCases.EnqueueKardexReport;
using ERP.Application.Inventory.UseCases.GetKardex;
using ERP.Application.Inventory.UseCases.RecalcularSnapshots;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.API.Controllers;

/// <summary>
/// Kardex valorizado de inventario por producto y bodega (mÃ©todo: promedio ponderado mÃ³vil).
/// Soporta modo sÃ­ncrono (respuesta inmediata) y asÃ­ncrono (202 + jobId).
/// </summary>
[AppFeature("Kardex", "perm:inventory.kardex.view", "ðŸ“Š", "/inventory/kardex", "perm:inventory.products.view", 42)]
[ApiController]
[Route("api/inventory/kardex")]
[Authorize]
[Produces("application/json")]
public sealed class KardexController : ControllerBase
{
    private readonly IMediator              _mediator;
    private readonly KardexOptions          _opts;
    private readonly ICurrentSubscriber         _tenant;
    private readonly IKardexReportRepository _reporteRepo;

    public KardexController(
        IMediator                  mediator,
        IOptions<KardexOptions>    opts,
        ICurrentSubscriber             tenant,
        IKardexReportRepository   reporteRepo)
    {
        _mediator    = mediator;
        _opts        = opts.Value;
        _tenant      = tenant;
        _reporteRepo = reporteRepo;
    }

    // â”€â”€ Kardex sÃ­ncrono / redirecciÃ³n automÃ¡tica a async â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Retorna el kardex valorizado de un producto en una bodega.
    /// Si el rango supera <c>MaxDaysForSync</c> y el modo asÃ­ncrono estÃ¡ activo,
    /// retorna 202 Accepted con un <c>jobId</c> para consultar el resultado mÃ¡s tarde.
    /// </summary>
    /// <remarks>
    /// Query params: productoId, bodegaId, fechaInicio (YYYY-MM-DD), fechaFin (YYYY-MM-DD).
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetKardex(CancellationToken ct = default)
    {
        var (ok, productId, warehouseId, startDate, endDate, err) = ParseQueryParams();
        if (!ok) return err!;

        // RedirecciÃ³n automÃ¡tica a async si el rango es muy largo
        if (ShouldUseAsync(startDate, endDate))
            return await EnqueueAsync(productId, warehouseId, startDate, endDate, ct);

        var result = await _mediator.Send(
            new GetKardexQuery(productId, warehouseId, startDate, endDate), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    // â”€â”€ Solicitar reporte asÃ­ncrono explÃ­citamente â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Solicita el kardex en modo asÃ­ncrono.
    /// Siempre retorna 202 Accepted con un <c>jobId</c>; el resultado se consulta en
    /// <c>GET /api/inventory/kardex/resultado/{jobId}</c>.
    /// </summary>
    [HttpPost("solicitar")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestKardex(CancellationToken ct = default)
    {
        var (ok, productId, warehouseId, startDate, endDate, err) = ParseQueryParams();
        if (!ok) return err!;

        return await EnqueueAsync(productId, warehouseId, startDate, endDate, ct);
    }

    // â”€â”€ Obtener resultado de un reporte asÃ­ncrono â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Retorna el resultado de un reporte de Kardex generado de forma asÃ­ncrona.
    /// </summary>
    /// <param name="jobId">ID del reporte obtenido en la solicitud.</param>
    /// <response code="200">Reporte completado.</response>
    /// <response code="202">Reporte aÃºn en proceso.</response>
    /// <response code="404">Reporte no encontrado.</response>
    /// <response code="400">El reporte terminÃ³ con error.</response>
    [HttpGet("resultado/{jobId:guid}")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetResult(Guid jobId, CancellationToken ct = default)
    {
        var reporte = await _reporteRepo.GetByIdAsync(_tenant.SubscriberId, jobId, ct);

        if (reporte is null)
            return NotFound(new { mensaje = "Reporte no encontrado." });

        if (reporte.Status == KardexReport.StatusPending ||
            reporte.Status == KardexReport.StatusProcessing)
        {
            Response.Headers.Append("Retry-After", "5");
            return Accepted(new
            {
                jobId   = reporte.Id,
                estado  = reporte.Status,
                mensaje = "El reporte aÃºn estÃ¡ siendo procesado. Consulte nuevamente en unos segundos.",
            });
        }

        if (reporte.Status == KardexReport.StatusError)
            return BadRequest(new { mensaje = reporte.ErrorMessage });

        // Completado: deserializar y retornar
        var kardex = reporte.ResultJson is not null
            ? JsonSerializer.Deserialize<KardexResponse>(reporte.ResultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : null;

        return kardex is null
            ? BadRequest(new { mensaje = "El resultado del reporte no pudo deserializarse." })
            : Ok(new { data = kardex, message = "OK" });
    }

    // â”€â”€ Exportar Excel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Exporta el kardex a Excel (.xlsx) con tabla formateada y resumen.</summary>
    [HttpGet("exportar/excel")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportExcel(CancellationToken ct = default)
    {
        var (ok, productId, warehouseId, startDate, endDate, err) = ParseQueryParams();
        if (!ok) return err!;

        var result = await _mediator.Send(
            new GetKardexQuery(productId, warehouseId, startDate, endDate), ct);

        if (!result.IsSuccess) return BadRequest(result.Error);

        var bytes    = KardexExcelExporter.Generate(result.Value!, startDate, endDate);
        var fileName = BuildFileName(result.Value!, startDate, endDate, "xlsx");

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // â”€â”€ Exportar PDF â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Exporta el kardex a PDF (A4 horizontal) con tabla y resumen.</summary>
    [HttpGet("exportar/pdf")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportPdf(CancellationToken ct = default)
    {
        var (ok, productId, warehouseId, startDate, endDate, err) = ParseQueryParams();
        if (!ok) return err!;

        var result = await _mediator.Send(
            new GetKardexQuery(productId, warehouseId, startDate, endDate), ct);

        if (!result.IsSuccess) return BadRequest(result.Error);

        var bytes    = KardexPdfExporter.Generate(result.Value!, startDate, endDate);
        var fileName = BuildFileName(result.Value!, startDate, endDate, "pdf");

        return File(bytes, "application/pdf", fileName);
    }

    // â”€â”€ Recalcular snapshots (administrador) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Recalcula los snapshots diarios del kardex bajo demanda.
    /// Ãštil para reconstruir el historial tras migraciÃ³n o correcciones manuales.
    /// Requiere rol SuperAdmin o Administrador.
    /// </summary>
    [HttpPost("recalcular")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecalcularSnapshots(
        [FromBody] RecalcularSnapshotsBody body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new RecalcularSnapshotsCommand(body.ProductId, body.WarehouseId, body.Until), ct);

        if (!result.IsSuccess)
            return BadRequest(new { mensaje = result.Error });
        return Ok(new { data = new { snapshotsGenerados = result.Value }, message = "OK" });
    }

    // â”€â”€ Helpers privados â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private (bool Ok, Guid ProductId, Guid WarehouseId,
             DateTime? StartDate, DateTime? EndDate,
             IActionResult? Error)
        ParseQueryParams()
    {
        if (!Request.Query.TryGetValue("productoId", out var pv) || !Guid.TryParse(pv, out var productId))
            return (false, default, default, null, null,
                BadRequest("productoId is required and must be a valid GUID."));

        if (!Request.Query.TryGetValue("bodegaId", out var bv) || !Guid.TryParse(bv, out var warehouseId))
            return (false, default, default, null, null,
                BadRequest("bodegaId is required and must be a valid GUID."));

        DateTime? startDate = null, endDate = null;
        if (Request.Query.TryGetValue("fechaInicio", out var fiv) && DateTime.TryParse(fiv, out var fi))
            startDate = fi;
        if (Request.Query.TryGetValue("fechaFin",    out var ffv) && DateTime.TryParse(ffv, out var ff))
            endDate = ff;

        return (true, productId, warehouseId, startDate, endDate, null);
    }

    private bool ShouldUseAsync(DateTime? fechaInicio, DateTime? fechaFin)
    {
        if (!_opts.UseScalableMode || !_opts.EnableAsyncReport) return false;
        if (fechaInicio is null || fechaFin is null) return false;

        var dias = (fechaFin.Value.Date - fechaInicio.Value.Date).Days;
        return dias > _opts.MaxDaysForSync;
    }

    private async Task<IActionResult> EnqueueAsync(
        Guid productId, Guid warehouseId,
        DateTime? startDate, DateTime? endDate,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new EnqueueKardexReportCommand(productId, warehouseId, startDate, endDate), ct);

        if (!result.IsSuccess)
            return BadRequest(new { mensaje = result.Error });

        Response.Headers.Append("Retry-After", "10");
        return Accepted(new
        {
            jobId   = result.Value!.JobId,
            estado  = result.Value.Status,
            mensaje = result.Value.Message,
        });
    }

    private static string BuildFileName(
        KardexResponse k, DateTime? from, DateTime? to, string ext)
    {
        var code   = k.Producto.Codigo.Replace("/", "-");
        var period = from.HasValue || to.HasValue
            ? $"_{from?.ToString("yyyyMMdd") ?? "start"}-{to?.ToString("yyyyMMdd") ?? "today"}"
            : "";
        return $"Kardex_{code}{period}.{ext}";
    }
}

/// <summary>Body del endpoint <c>POST /recalcular</c>.</summary>
public sealed record RecalcularSnapshotsBody(
    Guid? ProductId = null,
    Guid? WarehouseId   = null,
    DateTime? Until      = null);



