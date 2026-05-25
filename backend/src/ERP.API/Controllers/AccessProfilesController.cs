using ERP.API.Authorization;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Access.UseCases.Profiles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/admin/iam")]
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
    public async Task<IActionResult> GetProfiles([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var pfx = PermissionPolicyProvider.Prefix;
        var canProfiles = await _authorization.AuthorizeAsync(User, resource: null, policyName: $"{pfx}access.profiles.view");
        var canMembers = await _authorization.AuthorizeAsync(User, resource: null, policyName: $"{pfx}access.company_user_memberships.view");
        if (!canProfiles.Succeeded && !canMembers.Succeeded)
            return Forbid();

        var result = await _mediator.Send(new GetProfilesQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProfileDto>());
    }

    [HttpPost("profiles")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.profiles.view")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("profiles/{profileId:guid}")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.profiles.view")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromRoute] Guid profileId, [FromBody] UpdateProfileCommand command, CancellationToken ct)
    {
        if (profileId != command.ProfileId)
            return this.ApiBadRequest("ProfileId no coincide con la ruta.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profiles/{profileId:guid}/permissions")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.profiles.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertProfilePermissions([FromRoute] Guid profileId, [FromBody] UpsertProfilePermissionsCommand command, CancellationToken ct)
    {
        if (profileId != command.ProfileId)
            return this.ApiBadRequest("ProfileId no coincide con la ruta.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    [HttpGet("profiles/{profileId:guid}/permissions")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.profiles.view")]
    [ProducesResponseType(typeof(ApiResponse<ProfilePermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfilePermissions([FromRoute] Guid profileId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProfilePermissionsQuery(profileId), ct);
        return this.ToOkOrBadRequest(result);
    }
}
