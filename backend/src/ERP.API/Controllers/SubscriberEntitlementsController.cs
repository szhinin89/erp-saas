using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Subscriptions;
using ERP.Application.Subscriptions.UseCases.GetMyEntitlements;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Entitlements comerciales del tenant en sesión (snapshot único para UI).</summary>
[ApiController]
[AppFeature("SaaS Entitlements API", "perm:session.entitlements.api", "🧩", null, null, 986, IsVisibleInMenu = false)]
[Route("api/subscribers/entitlements")]
[Produces("application/json")]
public sealed class SubscriberEntitlementsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubscriberEntitlementsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Snapshot de módulos, features y límites del tenant actual (fail-closed sin suscripción activa).</summary>
    [HttpGet("me")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberEntitlementsSnapshot>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyEntitlementsQuery(), ct);
        return this.ToOkOrBadRequest(result);
    }
}
