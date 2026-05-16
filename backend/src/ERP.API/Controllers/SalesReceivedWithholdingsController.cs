using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Sales.DTOs;
using ERP.Application.Sales.UseCases.RetencionesRecibidas;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[AppFeature("Retenciones recibidas", "perm:ventas.retenciones-recibidas.list", "ðŸ“‹", "/ventas/retenciones-recibidas", "perm:ventas.facturas.view", 53)]
[ApiController]
[Route("api/ventas/retenciones-recibidas")]
[Authorize]
[Produces("application/json")]
public sealed class SalesReceivedWithholdingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesReceivedWithholdingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:ventas.retenciones-recibidas.list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SalesRetentionListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSalesRetentionsReceivedListQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SalesRetentionListItemDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:ventas.retenciones-recibidas.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterSalesRetentionCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Registrado");
    }
}

