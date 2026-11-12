using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    /// <summary>Devuelve los KPIs operativos de la empresa activa a una fecha de corte.</summary>
    /// <remarks>
    /// Incluye ventas del mes y del año, facturas del mes, cuentas por cobrar y por pagar pendientes y
    /// vencidas (importe y cantidad) y SKUs bajo mínimo o sin stock, junto con la fecha de corte, mes y
    /// año usados.
    /// </remarks>
    /// <param name="asOf">Fecha de corte (fecha de negocio <c>YYYY-MM-DD</c>); por defecto el día actual según la zona horaria de la empresa.</param>
    /// <response code="200">KPIs calculados a la fecha de corte.</response>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(ApiResponse<DashboardKpisDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpis(
        [FromQuery] DateOnly? asOf = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new GetDashboardKpisQuery(asOf), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
