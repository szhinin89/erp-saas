using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Extensions;
using ERP.Application.Modules.Dashboard;
using ERP.Application.Modules.Accounting.UseCases.ArAp;

namespace ERP.API.Controllers;

/// <summary>
/// Dashboard operativo del tenant: KPIs de ventas, CxC, CxP e inventario.
/// Todos los datos filtran automáticamente por el tenant y empresa del JWT.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// KPIs principales del dashboard: ventas MTD/YTD, CxC pendiente, CxP pendiente, stock bajo.
    /// </summary>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(DashboardKpisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetKpis(
        [FromQuery] Guid     companyId,
        [FromQuery] DateTime? asOf = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDashboardKpisQuery(companyId, asOf), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Reporte de antigüedad de Cuentas por Cobrar (AR aging) para una empresa.
    /// </summary>
    [HttpGet("ar-aging")]
    [ProducesResponseType(typeof(ArAgingReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetArAging(
        [FromQuery] Guid     companyId,
        [FromQuery] DateTime? asOf = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetArAgingReportQuery(companyId, asOf ?? DateTime.UtcNow), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Reporte de antigüedad de Cuentas por Pagar (AP aging) para una empresa.
    /// </summary>
    [HttpGet("ap-aging")]
    [ProducesResponseType(typeof(ApAgingReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetApAging(
        [FromQuery] Guid     companyId,
        [FromQuery] DateTime? asOf = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetApAgingReportQuery(companyId, asOf ?? DateTime.UtcNow), ct);
        return this.ToOkOrBadRequest(result);
    }
}
