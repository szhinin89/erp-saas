using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.MasterData.UseCases.PaymentTerms;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Condiciones de Pago", $"perm:{MasterDataPermissions.PaymentTermsView}", "💳", "/master/payment-terms", null, 58)]
[ApiController]
[Route("api/v1/master/payment-terms")]
[Authorize]
[Produces("application/json")]
public sealed class PaymentTermsController : ControllerBase
{
    private readonly IMediator _mediator;
    public PaymentTermsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsView}")]
    public async Task<IActionResult> List([FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await _mediator.Send(new ListPaymentTermsQuery(search), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsView}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetPaymentTermByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsManage}")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentTermCommand cmd, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct), "payment-term");

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsManage}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePaymentTermCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest("ID mismatch.");
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct), "Actualizado.");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsManage}")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnablePaymentTermCommand(id), ct), "Activado.");

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsManage}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisablePaymentTermCommand(id), ct), "Desactivado.");
}
