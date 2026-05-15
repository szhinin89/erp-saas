using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Sales.DTOs;
using ERP.Application.Sales.UseCases.RetencionesRecibidas;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[Modulo("Retenciones recibidas", "perm:ventas.retenciones-recibidas.list", "📋", "/ventas/retenciones-recibidas", "perm:ventas.facturas.view", 53)]
[ApiController]
[Route("api/ventas/retenciones-recibidas")]
[Authorize]
[Produces("application/json")]
public sealed class VentasRetencionesRecibidasController : ControllerBase
{
    private readonly IMediator _mediator;

    public VentasRetencionesRecibidasController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:ventas.retenciones-recibidas.list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<VentasRetencionRecibidaListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetVentasRetencionesRecibidasListQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<VentasRetencionRecibidaListItemDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:ventas.retenciones-recibidas.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Registrar([FromBody] RegistrarVentasRetencionRecibidaCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Registrado");
    }
}
