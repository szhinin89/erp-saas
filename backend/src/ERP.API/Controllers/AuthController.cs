using ERP.API.Attributes;
using ERP.API.Auth;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.CompletePasswordReset;
using ERP.Application.Auth.UseCases.ListMyCompanies;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Auth.UseCases.Logout;
using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Auth.UseCases.Reauthenticate;
using ERP.Application.Auth.UseCases.RefreshToken;
using ERP.Application.Auth.UseCases.SwitchCompany;
using ERP.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AccessibleCompanyDto = ERP.Application.Auth.DTOs.AccessibleCompanyDto;

namespace ERP.API.Controllers;

/// <summary>
/// Auth ERP único: login, refresh, sesión multi-empresa e impersonación platform.
/// Fase S1 (hardening): se eliminaron <c>POST /register</c> y <c>POST /password-reset</c>
/// (endpoints anónimos que aceptaban TenantId/Role o TenantId/Email/NewPassword directamente
/// del body, sin ningún control de identidad — hallazgos 5A/5B de la auditoría de cierre de
/// Access/IAM). El alta del primer usuario/tenant ya tiene un flujo seguro y dedicado:
/// <c>SetupController</c> (<c>POST /api/v1/setup/admin</c>, token de instalación de un solo uso,
/// nunca acepta TenantId/Role del cliente). El reseteo de contraseña sigue disponible solo por el
/// flujo oficial con token por email: <see cref="ForgotPassword"/> + <see cref="ResetPasswordWithToken"/>.
/// </summary>
[ApiController]
[AppFeature("Auth API", "perm:auth.api", "🧩", null, null, 988, IsVisibleInMenu = false)]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsSuccess)
        {
            if (
                result.Value?.RefreshToken is not null
                && result.Value.RefreshTokenExpiry is not null
            )
                AuthRefreshCookieHelper.SetRefreshCookie(
                    HttpContext,
                    result.Value.RefreshToken,
                    result.Value.RefreshTokenExpiry.Value
                );

            return this.ApiOk(result.Value);
        }

        return MapAuthFailure(result.Error);
    }

    /// <summary>
    /// Fase H — cierra el flujo de primer inicio de sesión: recibe el PasswordResetToken que
    /// Login devolvió cuando RequirePasswordReset estaba activo (sin JWT), fija la nueva
    /// contraseña y devuelve una sesión completa igual que un login exitoso.
    /// </summary>
    [HttpPost("complete-password-reset")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompletePasswordReset(
        [FromBody] CompletePasswordResetCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsSuccess)
        {
            if (
                result.Value?.RefreshToken is not null
                && result.Value.RefreshTokenExpiry is not null
            )
                AuthRefreshCookieHelper.SetRefreshCookie(
                    HttpContext,
                    result.Value.RefreshToken,
                    result.Value.RefreshTokenExpiry.Value
                );

            return this.ApiOk(result.Value);
        }

        return MapAuthFailure(result.Error);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh-ip")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest? request,
        CancellationToken cancellationToken
    )
    {
        var rawToken = AuthRefreshCookieHelper.ResolveRefreshToken(Request, request?.RefreshToken);
        if (string.IsNullOrWhiteSpace(rawToken))
            return this.ApiUnauthorized("Se requiere refresh token.");

        var result = await _mediator.Send(new RefreshTokenCommand(rawToken), cancellationToken);
        if (result.IsSuccess)
        {
            if (
                result.Value?.RefreshToken is not null
                && result.Value.RefreshTokenExpiry is not null
            )
                AuthRefreshCookieHelper.SetRefreshCookie(
                    HttpContext,
                    result.Value.RefreshToken,
                    result.Value.RefreshTokenExpiry.Value
                );

            return this.ApiOk(result.Value);
        }

        if (result.Code == ApiResponseCodes.Common.RateLimited)
            return this.ApiTooManyRequests(result.Error ?? "Demasiados intentos.");

        return this.ApiUnauthorized(result.Error ?? "Refresh token inválido.");
    }

    /// <summary>
    /// Fase 4: reautenticación del mismo usuario tras bloqueo por inactividad (SessionLockOverlay
    /// en frontend). Identidad SIEMPRE desde la cookie httpOnly, nunca del body — no acepta
    /// username ni refresh token editables, así el modal no puede usarse para reautenticar a otro
    /// usuario. <see cref="ReauthenticateHandler"/> documenta por qué la sesión resultante reinicia
    /// la ventana absoluta de 8 horas en vez de heredarla (a diferencia de <see cref="Refresh"/>).
    /// </summary>
    [HttpPost("reauthenticate")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh-ip")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reauthenticate(
        [FromBody] ReauthenticateRequest request,
        CancellationToken cancellationToken
    )
    {
        var rawToken = AuthRefreshCookieHelper.ResolveRefreshToken(Request, fromBody: null);
        if (string.IsNullOrWhiteSpace(rawToken))
            return this.ApiUnauthorized("Se requiere una sesión activa para reautenticar.");

        var result = await _mediator.Send(
            new ReauthenticateCommand(rawToken, request.Password),
            cancellationToken
        );
        if (result.IsSuccess)
        {
            if (
                result.Value?.RefreshToken is not null
                && result.Value.RefreshTokenExpiry is not null
            )
                AuthRefreshCookieHelper.SetRefreshCookie(
                    HttpContext,
                    result.Value.RefreshToken,
                    result.Value.RefreshTokenExpiry.Value
                );

            return this.ApiOk(result.Value);
        }

        return this.ApiUnauthorized(result.Error ?? "No se pudo reautenticar.");
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken
    )
    {
        var rawToken = AuthRefreshCookieHelper.ResolveRefreshToken(Request, request?.RefreshToken);
        var result = await _mediator.Send(
            new LogoutCommand(rawToken ?? string.Empty, request?.AllDevices ?? false),
            cancellationToken
        );

        AuthRefreshCookieHelper.ClearRefreshCookie(HttpContext);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPasswordWithToken(
        [FromBody] ResetPasswordWithTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("my-companies")]
    [Authorize]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<AccessibleCompanyDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> ListMyCompanies(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListMyCompaniesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("switch-company")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SwitchCompany(
        [FromBody] SwitchCompanyRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new SwitchCompanyCommand(request.CompanyId),
            cancellationToken
        );
        if (result.IsSuccess)
        {
            if (
                result.Value?.RefreshToken is not null
                && result.Value.RefreshTokenExpiry is not null
            )
                AuthRefreshCookieHelper.SetRefreshCookie(
                    HttpContext,
                    result.Value.RefreshToken,
                    result.Value.RefreshTokenExpiry.Value
                );

            return this.ApiOk(result.Value);
        }

        return this.ToOkOrBadRequest(result);
    }

    private IActionResult MapAuthFailure(string? error) =>
        this.ApiUnauthorized(error ?? "Unauthorized");
}
