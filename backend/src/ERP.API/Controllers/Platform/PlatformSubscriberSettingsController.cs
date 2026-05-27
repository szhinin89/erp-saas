using ERP.API.Contracts;
using ERP.API.Contracts.Platform;
using ERP.API.Extensions;
using ERP.Application.Subscribers.DTOs;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberCommercialProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

[ApiController]
[Route("api/platform/subscribers")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform")]
public sealed class PlatformSubscriberSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlatformSubscriberSettingsController(IMediator mediator) => _mediator = mediator;

    [HttpPatch("{subscriberId:guid}/company")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCompany(Guid subscriberId, [FromBody] UpdatePlatformSubscriberCompanyBody body, CancellationToken ct)
    {
        var command = new UpdateSubscriberCommercialProfileCommand(
            subscriberId, body.Name, body.Slug, body.DisplayOrder, body.Priority, body.PreferredLanguage);
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }
}
