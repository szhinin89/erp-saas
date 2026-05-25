using ERP.API.Auth;
using Microsoft.AspNetCore.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Auth.UseCases.Register;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Auth.UseCases.Logout;
using ERP.Application.Auth.UseCases.RefreshToken;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;

namespace ERP.API.Controllers;

[ApiController]
[AppFeature("Auth API", "perm:auth.api", "🧩", null, null, 988, IsVisibleInMenu = false)]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (result.IsSuccess)
        {
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                AuthRefreshCookieHelper.SetRefreshCookie(HttpContext, result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        return MapAuthFailure(result.Error);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh-ip")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? request, CancellationToken ct)
    {
        var rawToken = AuthRefreshCookieHelper.ResolveRefreshToken(Request, request?.RefreshToken);
        if (string.IsNullOrWhiteSpace(rawToken))
            return this.ApiUnauthorized("Se requiere refresh token.");

        var result = await _mediator.Send(new RefreshTokenCommand(rawToken), ct);
        if (result.IsSuccess)
        {
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                AuthRefreshCookieHelper.SetRefreshCookie(HttpContext, result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        if (result.ErrorCode == "rate_limited")
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new ApiResponse<AuthResponseDto?>(false, result.Error ?? "Demasiados intentos.", null));

        return this.ApiUnauthorized(result.Error ?? "Refresh token inválido.");
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        var rawToken = AuthRefreshCookieHelper.ResolveRefreshToken(Request, request?.RefreshToken);
        var result = await _mediator.Send(new LogoutCommand(rawToken ?? string.Empty, request?.AllDevices ?? false), ct);

        AuthRefreshCookieHelper.ClearRefreshCookie(HttpContext);
        return this.ToOkOrBadRequest(result);
    }

    private IActionResult MapAuthFailure(string? error)
    {
        if (!string.IsNullOrEmpty(error) &&
            error.StartsWith(DeploymentAuthMessages.ForbiddenPrefix, StringComparison.Ordinal))
        {
            var msg = error[DeploymentAuthMessages.ForbiddenPrefix.Length..].TrimStart();
            return this.ApiForbidden(msg);
        }

        return this.ApiUnauthorized(error ?? "Unauthorized");
    }
}
