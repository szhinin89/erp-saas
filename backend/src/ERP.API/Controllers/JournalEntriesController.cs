using CreateJournalEntryCommand = ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry.CreateJournalEntryCommand;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.UseCases.GetJournalEntries;
using ERP.Application.Modules.Accounting.UseCases.GetJournalEntryById;
using ERP.Application.Modules.Accounting.UseCases.VoidJournalEntry;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/finance/accounts/journal-entries")]
[Authorize]
[Produces("application/json")]
public sealed class JournalEntriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public JournalEntriesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:finance.journal.view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<JournalEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJournalEntries([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetJournalEntriesQuery(pageNumber, pageSize), ct);
        if (!result.IsSuccess)
            return this.ApiBadRequest(result.Error ?? "Error");
        if (result.Value is null)
            return this.ApiUnprocessableEntity("Respuesta de paginación inválida.");

        return this.ApiOk(new PagedResponse<JournalEntryDto>(
            Items: result.Value.Items,
            PageNumber: result.Value.PageNumber,
            PageSize: result.Value.PageSize,
            TotalCount: result.Value.TotalCount));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:finance.journal.view")]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJournalEntryById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetJournalEntryByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPatch("{id:guid}/void")]
    [Authorize(Policy = "perm:finance.journal.edit")]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidJournalEntry(Guid id, [FromBody] VoidJournalEntryCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID de la ruta no coincide con el ID del comando.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Anulado");
    }

    [HttpPost]
    [Authorize(Policy = "perm:finance.journal.create")]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateJournalEntry([FromBody] CreateJournalEntryCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }
}
