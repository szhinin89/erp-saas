using System.Text.Json;
using ERP.API.Contracts;
using ERP.API.Contracts.Inventory;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.RecalculateSnapshots;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Application.Inventory.UseCases.EnqueueKardexReport;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/inventory/kardex")]
[Authorize]
[Produces("application/json")]
public sealed class KardexJobsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentSubscriber _tenant;
    private readonly IKardexReportRepository _reporteRepo;

    public KardexJobsController(
        IMediator mediator,
        ICurrentSubscriber tenant,
        IKardexReportRepository reporteRepo)
    {
        _mediator = mediator;
        _tenant = tenant;
        _reporteRepo = reporteRepo;
    }

    [HttpPost("solicitar")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestKardex(CancellationToken ct = default)
    {
        var (ok, productId, warehouseId, startDate, endDate, err) = KardexQueryParameters.Parse(Request.Query);
        if (!ok) return err!;

        return await KardexAsyncHelper.EnqueueAsync(_mediator, Response, productId, warehouseId, startDate, endDate, ct);
    }

    [HttpGet("resultado/{jobId:guid}")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResult(Guid jobId, CancellationToken ct = default)
    {
        var reporte = await _reporteRepo.GetByIdAsync(_tenant.SubscriberId, jobId, ct);

        if (reporte is null)
            return NotFound(new { mensaje = "Reporte no encontrado." });

        if (reporte.Status == KardexReport.StatusPending || reporte.Status == KardexReport.StatusProcessing)
        {
            Response.Headers.Append("Retry-After", "5");
            return Accepted(new
            {
                jobId = reporte.Id,
                estado = reporte.Status,
                mensaje = "El reporte aún está siendo procesado. Consulte nuevamente en unos segundos.",
            });
        }

        if (reporte.Status == KardexReport.StatusError)
            return BadRequest(new { mensaje = reporte.ErrorMessage });

        var kardex = reporte.ResultJson is not null
            ? JsonSerializer.Deserialize<KardexResponse>(reporte.ResultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : null;

        return kardex is null
            ? BadRequest(new { mensaje = "El resultado del reporte no pudo deserializarse." })
            : Ok(new { data = kardex, message = "OK" });
    }

    [HttpPost("recalcular")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecalculateSnapshots([FromBody] RecalculateSnapshotsBody body, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RecalculateSnapshotsCommand(body.ProductId, body.WarehouseId, body.Until), ct);

        if (!result.IsSuccess)
            return BadRequest(new { mensaje = result.Error });
        return Ok(new { data = new { snapshotsGenerados = result.Value }, message = "OK" });
    }
}
