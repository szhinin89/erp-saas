using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.UseCases.ArAp;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/accounting/ar-ap")]
[Authorize]
[Produces("application/json")]
public sealed class ArApController : ControllerBase
{
    private readonly IMediator _mediator;

    public ArApController(IMediator mediator) => _mediator = mediator;

    [HttpPost("ar/entries")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateArEntry([FromBody] CreateArEntryCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Entrada CxC creada");
    }

    [HttpPost("ar/payments")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyArPayment([FromBody] ApplyArPaymentCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("ap/entries")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateApEntry([FromBody] CreateApEntryCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Entrada CxP creada");
    }

    [HttpPost("ap/payments")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyApPayment([FromBody] ApplyApPaymentCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}
