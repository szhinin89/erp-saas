using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Platform.Auth.UseCases.PlatformLogin;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>Autenticación canónica de operadores platform (SuperAdmin global).</summary>
[ApiController]
[Route("api/platform/auth")]
[Produces("application/json")]
public sealed class PlatformAuthController : ControllerBase
{
    private const string RefreshCookieName = "erp_refresh_token";

    private readonly IMediator _mediator;

    public PlatformAuthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Inicia sesión platform (identity_users, user_type=Platform).</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<ERP.Application.Auth.DTOs.AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] PlatformLoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return this.ApiUnauthorized(result.Error ?? "Credenciales inválidas.");

        if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
        {
            Response.Cookies.Append(RefreshCookieName, result.Value.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = result.Value.RefreshTokenExpiry.Value,
                Path = "/api/auth",
            });
        }

        return this.ApiOk(result.Value);
    }
}
