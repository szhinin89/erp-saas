using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Extensions;
using ERP.Application.Modules.Dashboard;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    [HttpGet("kpis")]
    [ProducesResponseType(typeof(DashboardKpisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetKpis(
        [FromQuery] DateTime? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetDashboardKpisQuery(asOf), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
