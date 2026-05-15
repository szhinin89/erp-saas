using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.Retenciones;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[Modulo("Retenciones compras", "perm:compras.retenciones.list", "ðŸ“‘", "/compras/retenciones", "perm:compras.facturas.view", 48)]
[ApiController]
[Route("api/compras/retenciones")]
[Authorize]
[Produces("application/json")]
public sealed class ComprasRetencionesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ComprasRetencionesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:compras.retenciones.list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<IssuedRetentionListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] Guid? proveedorId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCompraRetencionesEmitidasListQuery(proveedorId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<IssuedRetentionListItemDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:compras.retenciones.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Generar([FromBody] GenerarIssuedRetentionCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Generado");
    }

    [HttpPut("{id:guid}/enviar")]
    [Authorize(Policy = "perm:compras.retenciones.send")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Enviar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnviarIssuedRetentionCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enviado");
    }
}


