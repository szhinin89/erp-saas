using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Ventas.DTOs;
using ERP.Application.Ventas.UseCases.Notas;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[Modulo("Notas ventas", "perm:ventas.notas.list", "📃", "/ventas/notas", "perm:ventas.facturas.view", 51)]
[ApiController]
[Route("api/ventas/notas")]
[Authorize]
[Produces("application/json")]
public sealed class VentasNotasController : ControllerBase
{
    private readonly IMediator _mediator;

    public VentasNotasController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:ventas.notas.list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<VentasNotaListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid? facturaId,
        [FromQuery] string? estado,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetVentasNotasListQuery(facturaId, estado), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<VentasNotaListItemDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:ventas.notas.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Crear([FromBody] CrearVentasNotaCreditoDebitoCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}/enviar")]
    [Authorize(Policy = "perm:ventas.notas.send")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Enviar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnviarVentasNotaSriCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enviado");
    }
}
