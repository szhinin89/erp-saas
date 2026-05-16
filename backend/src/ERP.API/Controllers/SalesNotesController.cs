using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Sales.DTOs;
using ERP.Application.Sales.UseCases.Notas;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[AppFeature("Notas ventas", "perm:ventas.notas.list", "ðŸ“ƒ", "/ventas/notas", "perm:ventas.facturas.view", 51)]
[ApiController]
[Route("api/ventas/notas")]
[Authorize]
[Produces("application/json")]
public sealed class SalesNotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesNotesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:ventas.notas.list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SalesNoteListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? facturaId,
        [FromQuery] string? estado,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSalesNotesListQuery(facturaId, estado), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SalesNoteListItemDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:ventas.notas.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CrearSalesNoteCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}/enviar")]
    [Authorize(Policy = "perm:ventas.notas.send")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnviarSalesNotesriCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enviado");
    }
}

