using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Caja", $"perm:{CajaPermissions.View}", "💵", "/cash", null, 70)]
[ApiController]
[Route("api/v1/cash-sessions")]
[Authorize]
[Produces("application/json")]
public sealed class CashSessionController : ControllerBase
{
    private readonly IMediator _mediator;
    public CashSessionController(IMediator mediator) => _mediator = mediator;

    [HttpPost("open")]
    [Authorize(Policy = $"perm:{CajaPermissions.Open}")]
    public async Task<IActionResult> Open(
        [FromBody] OpenCashSessionCommand command, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = $"perm:{CajaPermissions.Close}")]
    public async Task<IActionResult> Close(
        Guid id, [FromBody] CloseCashSessionRequest request, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(
            new CloseCashSessionCommand(id, request.ClosingCounts, request.CloseNotes), ct));

    [HttpPost("{id:guid}/movements")]
    [Authorize(Policy = $"perm:{CajaPermissions.Record}")]
    public async Task<IActionResult> RecordMovement(
        Guid id, [FromBody] RecordCashMovementRequest request, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(
            new RecordCashMovementCommand(
                id, request.MovementType, request.Amount, request.Description,
                request.ReferenceType, request.ReferenceId, request.ReferenceNumber), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetCashSessionByIdQuery(id), ct));

    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => this.ToOkOrBadRequest(await _mediator.Send(
            new GetCashSessionListQuery(status, pageNumber, pageSize), ct), "OK");

    [HttpGet("my")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetMy(CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetMyCashSessionQuery(), ct), "OK");
}

public sealed record CloseCashSessionRequest(
    List<CashClosingCountInput> ClosingCounts,
    string? CloseNotes = null);

public sealed record RecordCashMovementRequest(
    string MovementType,
    decimal Amount,
    string Description,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? ReferenceNumber = null);
