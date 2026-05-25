using ERP.API.Authorization;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Access.UseCases.Profiles;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Navigation.UseCases.GetSessionMenu;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/admin/iam")]
[Produces("application/json")]
public sealed class AccessSessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccessSessionController(IMediator mediator) => _mediator = mediator;

    [HttpGet("me/menu")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SessionMenuGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessionMenu(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSessionMenuQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SessionMenuGroupDto>());
    }

    [HttpGet("me/permissions")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<MyPermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPermissions(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyPermissionsQuery(), ct);
        return this.ToOkOrBadRequest(result);
    }
}
