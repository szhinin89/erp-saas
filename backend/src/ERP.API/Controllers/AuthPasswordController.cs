using ERP.API.Auth;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.PasswordReset;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/Auth")]
[Produces("application/json")]
public sealed class AuthPasswordController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthPasswordController(IMediator mediator) => _mediator = mediator;

    [HttpPost("password-reset")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DirectPasswordReset([FromBody] DirectPasswordResetCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPasswordWithToken([FromBody] ResetPasswordWithTokenCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}
