using ERP.API.Authorization;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Access.UseCases.Profiles;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/admin/iam")]
[Produces("application/json")]
public sealed class AccessProfilesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _authorization;

    public AccessProfilesController(IMediator mediator, IAuthorizationService authorization)
    {
        _mediator = mediator;
        _authorization = authorization;
    }

    [HttpGet("profiles")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfiles([FromQuery] bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        var pfx = PermissionPolicyProvider.Prefix;
        var canProfiles = await _authorization.AuthorizeAsync(User, resource: null, policyName: $"{pfx}access.profiles.view");
        var canMembers = await _authorization.AuthorizeAsync(User, resource: null, policyName: $"{pfx}access.company_user_memberships.view");
        if (!canProfiles.Succeeded && !canMembers.Succeeded)
            return Forbid();

        var result = await _mediator.Send(new GetProfilesQuery(onlyActive), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProfileDto>());
    }

    [HttpPost("profiles")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    [HttpPut("profiles/{profileId:guid}")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromRoute] Guid profileId, [FromBody] UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        if (profileId != command.ProfileId)
            return this.ApiBadRequest("ProfileId no coincide con la ruta.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profiles/{profileId:guid}/permissions")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<PermissionUpsertResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertProfilePermissions(
        [FromRoute] Guid profileId,
        [FromBody] UpsertProfilePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        if (profileId != command.ProfileId)
            return this.ApiBadRequest("ProfileId no coincide con la ruta.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("profiles/{profileId:guid}/permissions")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<ProfilePermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfilePermissions([FromRoute] Guid profileId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetProfilePermissionsQuery(profileId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Audits profile permissions against the subscriber's commercial plan.
    /// Returns each assigned permission classified as: Effective, BlockedByPlan, or UnknownPrefix.
    /// Phantom permissions (BlockedByPlan) are stored in DB but never effective at runtime.
    /// Only accessible by admins (perm:access.profiles.view required).
    /// </summary>
    [HttpGet("profiles/{profileId:guid}/permission-audit")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<ProfilePermissionAuditDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfilePermissionAudit([FromRoute] Guid profileId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetProfilePermissionAuditQuery(profileId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
