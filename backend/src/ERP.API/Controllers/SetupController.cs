using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Setup.CreateInitialAdmin;
using ERP.Application.Setup.GetSetupStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>First-run ERP initialization endpoints. Both actions are anonymous and token-gated.</summary>
[ApiController]
[Route("api/v1/setup")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class SetupController : ControllerBase
{
    private readonly IMediator _mediator;

    public SetupController(IMediator mediator) => _mediator = mediator;

    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<SetupStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSetupStatusQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("admin")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateInitialAdmin(
        [FromBody] CreateInitialAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("ya ha sido inicializado") == true)
                return this.ApiConflict(result.Error);
            if (result.Error?.Contains("Token de inicialización inválido") == true)
                return this.ApiUnauthorized(result.Error);
        }
        return this.ToOkOrBadRequest(result);
    }
}
