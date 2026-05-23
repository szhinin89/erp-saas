using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Admin;
using ERP.Application.Admin.UseCases.SuperAdminGlobal;
using ERP.Application.Platform.Metrics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>
/// Platform Layer — métricas operativas del Control Plane.
/// Distribucion de suscriptores, planes y ciclo de vida.
/// </summary>
[ApiController]
[Route("api/platform/metrics")]
[Authorize(Roles = "SuperAdmin")]
[Tags("Platform")]
public sealed class PlatformMetricsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlatformMetricsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Métricas de la plataforma: suscriptores totales/activos/trial/gracia/suspendidos,
    /// distribución por plan y distribución de ciclo de vida.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PlatformMetricsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPlatformMetricsQuery(), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Series temporales de crecimiento (suscriptores, usuarios).</summary>
    [HttpGet("growth-analytics")]
    [ProducesResponseType(typeof(ApiResponse<GrowthAnalyticsResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrowthAnalytics(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? granularity,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminGrowthAnalyticsQuery(from, to, granularity), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("growth-analytics-monetary")]
    [ProducesResponseType(typeof(ApiResponse<GrowthMonetaryResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrowthMonetary(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? granularity,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminGrowthMonetaryQuery(from, to, granularity), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }
}
