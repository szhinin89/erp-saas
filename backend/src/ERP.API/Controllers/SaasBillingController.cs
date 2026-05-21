using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Billing.DTOs;
using ERP.Application.Billing.UseCases.GetBillingAccount;
using ERP.Application.Billing.UseCases.ListBillingEvents;
using ERP.Application.Billing.UseCases.ListBillingInvoices;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Facturación SaaS", "perm:saas.billing.view", "💳", "/saas/billing", null, 27, IsVisibleInMenu = false)]
[ApiController]
[Route("api/saas/billing")]
[Authorize]
[Produces("application/json")]
public sealed class SaasBillingController : ControllerBase
{
    private readonly IMediator _mediator;

    public SaasBillingController(IMediator mediator) => _mediator = mediator;

    [HttpGet("account")]
    [Authorize(Policy = "perm:saas.billing.view")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberBillingAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccount(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBillingAccountQuery(), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("invoices")]
    [Authorize(Policy = "perm:saas.billing.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SaasBillingInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvoices([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListBillingInvoicesQuery(take), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SaasBillingInvoiceDto>());
    }

    [HttpGet("events")]
    [Authorize(Policy = "perm:saas.billing.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BillingEventDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListEvents([FromQuery] int take = 100, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListBillingEventsQuery(take), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<BillingEventDto>());
    }
}
