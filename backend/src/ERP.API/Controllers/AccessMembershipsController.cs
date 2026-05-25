using ERP.API.Controllers.Platform;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembership;
using ERP.Application.Access.UseCases.SubscriberAccess;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembership;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/admin/iam")]
[Produces("application/json")]
public sealed class AccessMembershipsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccessMembershipsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("company_user_memberships/grant")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GrantCompanyUserMembership([FromBody] UpsertCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    [HttpPost("company_user_memberships/revoke")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeCompanyUserMembership([FromBody] RevokeCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    [HttpGet("subscriber/company_user_memberships")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriberCompanyUserMemberships([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSubscriberCompanyUserMembershipsQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SubscriberCompanyUserMembershipItemDto>());
    }

    [HttpPost("subscriber/company_user_memberships")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertSubscriberCompanyUserMembership([FromBody] SubscriberUpsertCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    [HttpPost("subscriber/company_user_memberships/revoke")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeSubscriberCompanyUserMembership([FromBody] SubscriberRevokeCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }
}
