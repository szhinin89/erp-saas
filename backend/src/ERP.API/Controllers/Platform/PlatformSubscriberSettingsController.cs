using ERP.API.Contracts;
using ERP.API.Contracts.Platform;
using ERP.API.Extensions;
using ERP.Application.Subscribers.DTOs;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberGlobalParameters;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberOperationalSettings;
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
            subscriberId, body.Name, body.Slug, body.Ruc, body.ShortName,
            body.TradeName, body.Dinardap, body.LogoUrl, body.DisplayOrder, body.Priority);
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpPatch("{subscriberId:guid}/global-parameters")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGlobalParameters(Guid subscriberId, [FromBody] UpdatePlatformGlobalParametersBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSubscriberGlobalParametersCommand(subscriberId, body.ElectronicBillingTrialEnabled), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{subscriberId:guid}/operational-settings")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOperationalSettings(Guid subscriberId, [FromBody] UpdatePlatformOperationalSettingsBody body, CancellationToken ct)
    {
        var command = new UpdateSubscriberOperationalSettingsCommand(
            subscriberId, body.Currency, body.Language, body.Timezone, body.InvoicePrefix, body.DefaultCreditDays);
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }
}
