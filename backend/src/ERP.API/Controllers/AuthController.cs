using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using AccessibleCompanyDto = ERP.Application.Auth.DTOs.AccessibleCompanyDto;
using ERP.API.Extensions;
using ERP.Application.Auth.UseCases.Register;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Auth.UseCases.Logout;
using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Auth.UseCases.RefreshToken;
using ERP.Application.Auth.UseCases.SuperAdminLogin;
using ERP.Application.Auth.UseCases.SwitchSubscriber;
using ERP.Application.Auth.UseCases.SwitchCompany;
using ERP.Application.Auth.UseCases.ListMyCompanies;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;

namespace ERP.API.Controllers;

/// <summary>
/// Registro e inicio de sesión de usuarios.
/// No requiere autenticación previa.
/// </summary>
[ApiController]
[AppFeature("Auth API", "perm:auth.api", "🧩", null, null, 988, IsVisibleInMenu = false)]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "erp_refresh_token";

    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    private void SetRefreshCookie(string rawToken, DateTime expiry)
    {
        Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly  = true,
            Secure    = !HttpContext.Request.IsHttps ? false : true,
            SameSite  = SameSiteMode.Strict,
            Expires   = expiry,
            Path      = "/api/auth",
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure   = !HttpContext.Request.IsHttps ? false : true,
            SameSite = SameSiteMode.Strict,
            Path     = "/api/auth",
        });
    }

    private string? ResolveRefreshToken(string? fromBody)
        => string.IsNullOrWhiteSpace(fromBody)
            ? Request.Cookies[RefreshCookieName]
            : fromBody;

    /// <summary>Registra un nuevo usuario en un tenant existente.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Inicia sesión y retorna un JWT Bearer.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (result.IsSuccess)
        {
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                SetRefreshCookie(result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        return MapAuthFailure(result.Error);
    }

    /// <summary>Recuperación de contraseña directa (modo Direct del tenant).</summary>
    [HttpPost("password-reset")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DirectPasswordReset([FromBody] DirectPasswordResetCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Solicita enlace de restablecimiento; solo requiere email.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Completa el restablecimiento con el token recibido por email.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetPasswordWithToken([FromBody] ResetPasswordWithTokenCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Login global de SuperAdmin: email + password (sin SubscriberId).</summary>
    [HttpPost("superadmin-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SuperAdminLogin([FromBody] SuperAdminLoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (result.IsSuccess)
            return this.ApiOk(result.Value);

        return MapAuthFailure(result.Error);
    }

    /// <summary>Renueva el access token usando un refresh token válido.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? request, CancellationToken ct)
    {
        var rawToken = ResolveRefreshToken(request?.RefreshToken);
        if (string.IsNullOrWhiteSpace(rawToken))
            return this.ApiUnauthorized("Se requiere refresh token.");

        var result = await _mediator.Send(new RefreshTokenCommand(rawToken), ct);
        if (result.IsSuccess)
        {
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                SetRefreshCookie(result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        return this.ApiUnauthorized("Refresh token inválido.");
    }

    /// <summary>Cierra la sesión revocando el refresh token.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        var rawToken = ResolveRefreshToken(request?.RefreshToken);
        var result = await _mediator.Send(new LogoutCommand(rawToken ?? string.Empty, request?.AllDevices ?? false), ct);

        ClearRefreshCookie();
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Lista empresas operativas accesibles en el suscriptor activo.</summary>
    [HttpGet("my-companies")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccessibleCompanyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyCompanies(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListMyCompaniesQuery(), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Cambia la empresa operativa (company_id) en el JWT.</summary>
    [HttpPost("switch-company")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SwitchCompany([FromBody] SwitchCompanyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SwitchCompanyCommand(request.CompanyId), ct);
        if (result.IsSuccess)
        {
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                SetRefreshCookie(result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Cambia el subscriber_id del JWT para el SuperAdmin.</summary>
    [HttpPost("switch-subscriber")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SwitchSubscriber([FromBody] SwitchSubscriberCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
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
