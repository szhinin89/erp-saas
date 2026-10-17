using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Caja", $"perm:{CajaPermissions.View}", "💵", "/treasury/cash", null, 70)]
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
        [FromBody] OpenCashSessionCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = $"perm:{CajaPermissions.Close}")]
    public async Task<IActionResult> Close(
        Guid id,
        [FromBody] CloseCashSessionRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CloseCashSessionCommand(id, request.ClosingCounts, request.CloseNotes),
                ct
            )
        );

    [HttpPost("{id:guid}/movements")]
    [Authorize(Policy = $"perm:{CajaPermissions.Record}")]
    public async Task<IActionResult> RecordMovement(
        Guid id,
        [FromBody] RecordCashMovementRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new RecordCashMovementCommand(
                    id,
                    request.MovementType,
                    request.ReasonId,
                    request.Amount,
                    request.Description,
                    request.ReferenceType,
                    request.ReferenceId,
                    request.ReferenceNumber,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>
    /// ACCOUNTING-12C — depósito del efectivo de la caja en una cuenta bancaria de la empresa
    /// (movimiento BankDeposit; asiento Debe banco / Haber caja). Mismo permiso que registrar
    /// movimientos de caja: es una salida de efectivo del turno que opera el cajero.
    /// </summary>
    [HttpPost("{id:guid}/bank-deposits")]
    [Authorize(Policy = $"perm:{CajaPermissions.Record}")]
    public async Task<IActionResult> RecordBankDeposit(
        Guid id,
        [FromBody] RecordCashBankDepositRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new RecordCashBankDepositCommand(
                    id,
                    request.CompanyBankAccountId,
                    request.Amount,
                    request.Description,
                    request.ReferenceNumber,
                    request.ClientRequestId
                ),
                ct
            )
        );

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashSessionByIdQuery(id), ct));

    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCashSessionListQuery(status, pageNumber, pageSize), ct),
            "OK"
        );

    [HttpGet("my")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetMy(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetMyCashSessionQuery(), ct), "OK");

    [HttpGet("{id:guid}/collection-summary")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetCollectionSummary(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashSessionCollectionSummaryQuery(id), ct));
}

public sealed record CloseCashSessionRequest(
    List<CashClosingCountInput> ClosingCounts,
    string? CloseNotes = null
);

public sealed record RecordCashMovementRequest(
    string MovementType,
    Guid ReasonId,
    decimal Amount,
    string Description,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? ReferenceNumber = null,
    Guid ClientRequestId = default
);

public sealed record RecordCashBankDepositRequest(
    Guid CompanyBankAccountId,
    decimal Amount,
    string Description,
    string? ReferenceNumber = null,
    Guid ClientRequestId = default
);
