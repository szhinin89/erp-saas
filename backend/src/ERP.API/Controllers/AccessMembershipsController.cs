using ERP.API.Controllers.Platform;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.PlatformSubscribers;
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

    [HttpPost("register-subscriber")]
    [Obsolete("Legacy IAM route register-tenant. Prefer register-subscriber.")]
    [HttpPost("register-tenant")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterTenant([FromBody] PlatformCreateSubscriberWithAdminCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

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
    [Obsolete("Legacy IAM route segment 'tenant'. Prefer subscriber/company_user_memberships.")]
    [HttpGet("tenant/company_user_memberships")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriberCompanyUserMemberships([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSubscriberCompanyUserMembershipsQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SubscriberCompanyUserMembershipItemDto>());
    }

    [HttpPost("subscriber/company_user_memberships")]
    [Obsolete("Legacy IAM route segment 'tenant'. Prefer subscriber/company_user_memberships.")]
    [HttpPost("tenant/company_user_memberships")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertTenantCompanyUserMembership([FromBody] SubscriberUpsertCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    [HttpPost("subscriber/company_user_memberships/revoke")]
    [Obsolete("Legacy IAM route segment 'tenant'. Prefer subscriber/company_user_memberships/revoke.")]
    [HttpPost("tenant/company_user_memberships/revoke")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeTenantCompanyUserMembership([FromBody] SubscriberRevokeCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }
}
