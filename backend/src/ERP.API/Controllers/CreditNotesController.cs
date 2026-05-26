using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Sales.DTOs;
using ERP.Application.Sales.UseCases.SalesNotes;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[AppFeature("SalesNotes ventas", "perm:sales.credit-notes.view", "📃", "/sales/credit-notes", "perm:sales.invoices.view", 51)]
[ApiController]
[Route("api/sales/credit-notes")]
[Authorize]
[Produces("application/json")]
public sealed class CreditNotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CreditNotesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:sales.credit-notes.view")]
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
    [Authorize(Policy = "perm:sales.credit-notes.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateSalesNoteCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}/enviar")]
    [Authorize(Policy = "perm:sales.credit-notes.send")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SendSalesNoteSriCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enviado");
    }
}
