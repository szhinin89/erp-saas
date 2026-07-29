using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Session.DTOs;
using ERP.Application.Modules.Session.UseCases.GetMyAvailableBranches;
using ERP.Application.Modules.Session.UseCases.GetSessionContext;
using ERP.Application.Modules.Session.UseCases.SwitchBranch;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/session")]
[Produces("application/json")]
public sealed class SessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("context")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<SessionContextDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContext(CancellationToken cancellationToken)
    {
        Response.Headers["Cache-Control"] = "no-store";

        var result = await _mediator.Send(new GetSessionContextQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("switch-branch")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<SessionBranchDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SwitchBranch(
        [FromBody] SwitchBranchRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new SwitchBranchCommand(request.BranchId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("available-branches")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<MyAvailableBranchesDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableBranches(CancellationToken cancellationToken)
    {
        Response.Headers["Cache-Control"] = "no-store";

        var result = await _mediator.Send(new GetMyAvailableBranchesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
