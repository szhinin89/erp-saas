using ERP.API.Extensions;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/sales-receivables")]
[Authorize]
[Produces("application/json")]
public sealed class SalesReceivablesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesReceivablesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("by-invoice/{invoiceId:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetByInvoice(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetReceivableByInvoiceQuery(invoiceId), ct));

    [HttpGet]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetReceivablesListQuery(search, status, pageNumber, pageSize),
                ct
            ),
            "OK"
        );
}
