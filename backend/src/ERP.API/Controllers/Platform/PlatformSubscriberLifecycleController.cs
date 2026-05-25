using ERP.API.Contracts;
using ERP.API.Contracts.Platform;
using ERP.API.Extensions;
using ERP.Application.Access.UseCases.PlatformSubscribers;
using ERP.Application.Platform.Subscribers.UseCases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

[ApiController]
[Route("api/platform/subscribers")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform")]
public sealed class PlatformSubscriberLifecycleController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlatformSubscriberLifecycleController(IMediator mediator) => _mediator = mediator;

    [HttpPatch("{subscriberId:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(Guid subscriberId, [FromBody] SubscriberLifecycleNoteBody? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ActivateSubscriberCommand(subscriberId, body?.Notes), ct);
        return this.ToOkOrBadRequest(result, "Activado");
    }

    [HttpPatch("{subscriberId:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Suspend(Guid subscriberId, [FromBody] SubscriberSuspendBody? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SuspendSubscriberCommand(subscriberId, body?.Reason), ct);
        return this.ToOkOrBadRequest(result, "Suspendido");
    }

    [HttpPatch("{subscriberId:guid}/trial")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetTrial(Guid subscriberId, [FromBody] SubscriberTrialBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetSubscriberTrialCommand(subscriberId, body.TrialEndsAtUtc), ct);
        return this.ToOkOrBadRequest(result, "Trial iniciado");
    }

    [HttpPatch("{subscriberId:guid}/grace-period")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetGracePeriod(Guid subscriberId, [FromBody] SubscriberGracePeriodBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetSubscriberGracePeriodCommand(subscriberId, body.GracePeriodEndsAtUtc, body.Reason), ct);
        return this.ToOkOrBadRequest(result, "Período de gracia iniciado");
    }

    [HttpPatch("{subscriberId:guid}/plan")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePlan(Guid subscriberId, [FromBody] SubscriberChangePlanBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangePlatformSubscriberPlanCommand(subscriberId, body.NewPlanCode, body.Notes), ct);
        return this.ToOkOrBadRequest(result, "Plan actualizado");
    }
}
