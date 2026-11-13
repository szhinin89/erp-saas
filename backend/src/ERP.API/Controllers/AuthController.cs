using ERP.API.Attributes;
using ERP.API.Auth;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.CompletePasswordReset;
using ERP.Application.Auth.UseCases.GlobalLogin;
using ERP.Application.Auth.UseCases.ListMyCompanies;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Auth.UseCases.Logout;
using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Auth.UseCases.Reauthenticate;
using ERP.Application.Auth.UseCases.RefreshToken;
using ERP.Application.Auth.UseCases.SwitchCompany;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
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

    /// <summary>
    /// Inicia sesión de un usuario de empresa con usuario y contraseña.
    /// </summary>
    /// <remarks>
    /// Operación pública por configuración actual (el controlador no declara <c>[Authorize]</c>). Pasa antes por el
    /// control de intentos de login (SEC-A01): al exceder el límite responde 429 y, si el almacén de límites no
    /// está disponible, 503. Un login exitoso emite la cookie httpOnly de refresh cuando la respuesta trae token de
    /// refresh. Si el usuario debe cambiar su contraseña, la respuesta no trae sesión completa sino el token de
    /// restablecimiento que se usa en <c>complete-password-reset</c>. Un usuario con varias empresas recibe
    /// <c>requiresCompanySelection</c> y debe elegir empresa con <c>switch-company</c>. Todo fallo de credenciales
    /// o de estado de la cuenta sale como 401.
    /// </remarks>
    /// <param name="command">Usuario, contraseña y datos del inicio de sesión.</param>
    /// <param name="loginThrottle">Servicio de control de intentos (inyectado; no forma parte del contrato).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sesión iniciada; incluye el token de acceso o, si corresponde, el token de restablecimiento de contraseña.</response>
    /// <response code="409">Conflicto al registrar la sesión del usuario.</response>
    /// <response code="429">Se superó el límite de intentos de login para el usuario o la dirección IP.</response>
    /// <response code="503">El almacén de límites de login no está disponible; el intento no se procesó.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        [FromServices] ILoginAttemptThrottle loginThrottle,
        CancellationToken cancellationToken
    ) => ThrottledLoginAsync(loginThrottle, command.Username, command, cancellationToken);

    /// <summary>
    /// Inicia sesión como administrador global (token con tenant vacío).
    /// </summary>
    /// <remarks>
    /// Operación pública. Comparte con <c>login</c> el control de intentos (429 al exceder el límite, 503 si no
    /// está disponible el almacén de límites). Solo autoriza a usuarios con rol global de administrador; el token
    /// resultante no sirve para endpoints operativos: primero se debe pasar por <c>auth/global/operate-company</c>.
    /// Emite la cookie httpOnly de refresh cuando la respuesta trae token de refresh. Las credenciales inválidas, el
    /// usuario inactivo o sin rol de administrador global responden 401; esa respuesta no se declara en el contrato
    /// porque el contrato central solo documenta 401 en operaciones que exigen token.
    /// </remarks>
    /// <param name="command">Usuario y contraseña del administrador global.</param>
    /// <param name="loginThrottle">Servicio de control de intentos (inyectado; no forma parte del contrato).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sesión global iniciada.</response>
    /// <response code="429">Se superó el límite de intentos de login para el usuario o la dirección IP.</response>
    /// <response code="503">El almacén de límites de login no está disponible; el intento no se procesó.</response>
    [HttpPost("global-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> GlobalLogin(
        [FromBody] GlobalLoginCommand command,
        [FromServices] ILoginAttemptThrottle loginThrottle,
        CancellationToken cancellationToken
    ) => ThrottledLoginAsync(loginThrottle, command.Username, command, cancellationToken);

    /// <summary>
    /// SEC-A01 — admisión compartida de login y global-login ANTES del handler (sin cambiarlo):
    /// límite excedido o cola llena → 429 <c>RATE_LIMITED</c>; Redis de límites no disponible → 503
    /// <c>LOGIN_THROTTLE_UNAVAILABLE</c>. Solo un login exitoso devuelve el cupo reservado.
    /// </summary>
    private async Task<IActionResult> ThrottledLoginAsync(
        ILoginAttemptThrottle loginThrottle,
        string username,
        IRequest<Result<AuthResponseDto>> command,
        CancellationToken cancellationToken
    )
    {
        using var attempt = await loginThrottle.BeginAsync(
            username ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken
        );
        switch (attempt.Status)
        {
            case LoginAdmissionStatus.RateLimited:
                return this.ApiFailure(
                    Result<AuthResponseDto>.Failure(
                        "Demasiados intentos.",
                        ApiResponseCodes.Common.RateLimited
                    )
                );
            case LoginAdmissionStatus.Unavailable:
                return this.ApiFailure(
                    Result<AuthResponseDto>.Failure(
                        "Límites de login no disponibles.",
                        ApiResponseCodes.Auth.LoginThrottleUnavailable
                    )
                );
        }

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsSuccess)
        {
            await attempt.MarkSucceededAsync();
            AuthRefreshCookieHelper.SetRefreshCookieIfIssued(HttpContext, result.Value);
            return this.ApiOk(result.Value);
        }

        return this.ApiFailure(result, ApiResponseCodes.Common.Unauthorized);
    }

    /// <summary>
    /// Completa el cambio de contraseña obligatorio del primer inicio de sesión.
    /// </summary>
    /// <remarks>
    /// Operación pública. Recibe el token de restablecimiento que <c>login</c> devolvió cuando el usuario debía
    /// cambiar su contraseña (sin JWT), fija la nueva contraseña y devuelve una sesión completa, igual que un login
    /// exitoso, con cookie de refresh. Un token inválido o vencido responde 401; esa respuesta no se declara en el
    /// contrato porque el contrato central solo documenta 401 en operaciones que exigen token.
    /// </remarks>
    /// <param name="command">Token de restablecimiento y nueva contraseña.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contraseña actualizada y sesión iniciada.</response>
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
            AuthRefreshCookieHelper.SetRefreshCookieIfIssued(HttpContext, result.Value);
            return this.ApiOk(result.Value);
        }

        return this.ApiFailure(result, ApiResponseCodes.Common.Unauthorized);
    }

    /// <summary>
    /// Renueva la sesión con el refresh token.
    /// </summary>
    /// <remarks>
    /// Operación pública con límite por IP (429). El refresh token se toma del cuerpo y, si no viene, de la cookie
    /// httpOnly de refresh. Rota el token de refresh y vuelve a emitir la cookie. Sin token responde 401, igual que
    /// un token inválido, revocado o vencido, o un usuario, empresa o membresía que ya no son válidos.
    /// </remarks>
    /// <param name="request">Refresh token opcional; si se omite se usa la cookie de refresh.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sesión renovada con un nuevo token de acceso.</response>
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
            AuthRefreshCookieHelper.SetRefreshCookieIfIssued(HttpContext, result.Value);
            return this.ApiOk(result.Value);
        }

        // RATE_LIMITED → 429 por la tabla única; cualquier otro fallo de refresh sin código → 401.
        return this.ApiFailure(result, ApiResponseCodes.Common.Unauthorized);
    }

    /// <summary>
    /// Reautentica al mismo usuario tras un bloqueo por inactividad.
    /// </summary>
    /// <remarks>
    /// Operación pública con límite por IP (429). La identidad sale siempre de la cookie httpOnly de refresh, nunca
    /// del cuerpo: solo se envía la contraseña, de modo que no sirve para autenticar a otro usuario. La sesión
    /// resultante reinicia la ventana absoluta de sesión y renueva la cookie. Sin cookie o con contraseña
    /// incorrecta responde 401.
    /// </remarks>
    /// <param name="request">Contraseña del usuario de la sesión bloqueada.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sesión reautenticada con un nuevo token de acceso.</response>
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
            AuthRefreshCookieHelper.SetRefreshCookieIfIssued(HttpContext, result.Value);
            return this.ApiOk(result.Value);
        }

        return this.ApiFailure(result, ApiResponseCodes.Common.Unauthorized);
    }

    /// <summary>
    /// Cierra la sesión actual o todas las sesiones del usuario.
    /// </summary>
    /// <remarks>
    /// Operación pública. El refresh token se toma del cuerpo o de la cookie. Con <c>allDevices</c> en verdadero
    /// revoca todas las sesiones del usuario; si no, solo la actual. Siempre borra la cookie de refresh del
    /// navegador. Sin refresh token, o con uno inválido, responde 400.
    /// </remarks>
    /// <param name="request">Refresh token opcional (si se omite se usa la cookie) e indicador de cierre en todos los dispositivos.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sesión cerrada.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
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

    /// <summary>
    /// Solicita el correo de restablecimiento de contraseña.
    /// </summary>
    /// <remarks>
    /// Operación pública con límite por IP (429). La respuesta es neutral: devuelve 200 exista o no la cuenta, para
    /// no revelar qué correos están registrados; el límite por correo también se aplica sin revelar su efecto. El
    /// enlace con el token se envía por correo.
    /// </remarks>
    /// <param name="command">Correo de la cuenta.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Solicitud aceptada (la respuesta es la misma exista o no la cuenta).</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-forgot-password-ip")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Restablece la contraseña con el token recibido por correo.
    /// </summary>
    /// <remarks>
    /// Operación pública. Consume el token de un solo uso del flujo <c>forgot-password</c> y fija la nueva
    /// contraseña. Un token inválido o vencido responde 400.
    /// </remarks>
    /// <param name="command">Token recibido por correo y nueva contraseña.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contraseña restablecida.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordWithToken(
        [FromBody] ResetPasswordWithTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Lista las empresas a las que el usuario autenticado tiene acceso.
    /// </summary>
    /// <remarks>
    /// Solo incluye empresas con membresía activa del usuario; sirve para la selección de empresa tras el login.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Empresas accesibles del usuario.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    [HttpGet("my-companies")]
    [Authorize]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<AccessibleCompanyDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListMyCompanies(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListMyCompaniesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Cambia la empresa activa de la sesión.
    /// </summary>
    /// <remarks>
    /// Emite un nuevo token de acceso para la empresa elegida y renueva la cookie de refresh. Exige membresía
    /// activa en esa empresa.
    /// </remarks>
    /// <param name="request">Id de la empresa a activar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sesión emitida para la empresa elegida.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("switch-company")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
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
            AuthRefreshCookieHelper.SetRefreshCookieIfIssued(HttpContext, result.Value);
            return this.ApiOk(result.Value);
        }

        return this.ToOkOrBadRequest(result);
    }
}
