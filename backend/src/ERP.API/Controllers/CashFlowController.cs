using ERP.API.Extensions;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.UseCases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/cash/bank")]
[Authorize]
[Produces("application/json")]
public sealed class CashFlowController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashFlowController(IMediator mediator) => _mediator = mediator;

    [HttpGet("flujo-efectivo/real")]
    [Authorize(Policy = "perm:cash.bank.flujo.view")]
    public async Task<IActionResult> GetActualCashFlow([FromQuery] DateTime desde, [FromQuery] DateTime hasta, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetFlujoEfectivoRealQuery(desde, hasta), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<DailyCashFlowDto>());
    }
}
