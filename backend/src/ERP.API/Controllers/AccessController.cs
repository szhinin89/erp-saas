using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.BootstrapLogin;
using ERP.Application.Access.UseCases.SwitchSubscriber;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Identity &amp; Access Management (IAM) — bootstrap y switch de sesión.
/// Perfiles, membresías y menú: controllers satélite en la misma ruta base.
/// </summary>
[ApiController]
[AppFeature("Access IAM API", "perm:admin.iam.api", "🧩", null, null, 983, IsVisibleInMenu = false)]
[Route("api/admin/iam")]
[Produces("application/json")]
public class AccessController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccessController(IMediator mediator) => _mediator = mediator;

    [HttpPost("bootstrap-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BootstrapLoginResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BootstrapLogin([FromBody] BootstrapLoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrUnauthorized(result);
    }

    [HttpPost("switch-subscriber")]
    [Authorize(Policy = "Bootstrap")]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SwitchSubscriber([FromBody] SwitchSubscriberCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}
