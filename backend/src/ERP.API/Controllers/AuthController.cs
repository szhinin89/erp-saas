using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Auth.UseCases.Register;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Auth.UseCases.Logout;
using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Auth.UseCases.RefreshToken;
using ERP.Application.Auth.UseCases.SuperAdminLogin;
using ERP.Application.Auth.UseCases.SwitchTenant;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;

namespace ERP.API.Controllers;

/// <summary>
/// Registro e inicio de sesión de usuarios.
/// No requiere autenticación previa.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    // ── Cookie helper ─────────────────────────────────────────────────────

    private const string RefreshCookieName = "erp_refresh_token";

    /// <summary>
    /// Establece el refresh token como cookie httpOnly (no accesible desde JS → mitiga XSS).
    /// Secure=true en producción, SameSite=Strict previene CSRF.
    /// </summary>
    private void SetRefreshCookie(string rawToken, DateTime expiry)
    {
        Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly  = true,
            Secure    = !HttpContext.Request.IsHttps ? false : true, // Secure en HTTPS
            SameSite  = SameSiteMode.Strict,
            Expires   = expiry,
            Path      = "/api/auth", // solo endpoints de auth pueden leer la cookie
        });
    }

    /// <summary>Elimina la cookie del refresh token.</summary>
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

    /// <summary>Intenta leer el refresh token de la cookie httpOnly; si no hay, usa el body.</summary>
    private string? ResolveRefreshToken(string? fromBody)
        => string.IsNullOrWhiteSpace(fromBody)
            ? Request.Cookies[RefreshCookieName]
            : fromBody;

    private readonly RegisterHandler        _registerHandler;
    private readonly LoginHandler           _loginHandler;
    private readonly RefreshTokenHandler    _refreshTokenHandler;
    private readonly LogoutHandler          _logoutHandler;
    private readonly PasswordResetHandler   _passwordResetHandler;
    private readonly SuperAdminLoginHandler _superAdminLoginHandler;
    private readonly SwitchTenantHandler    _switchTenantHandler;

    public AuthController(
        RegisterHandler registerHandler,
        LoginHandler loginHandler,
        RefreshTokenHandler refreshTokenHandler,
        LogoutHandler logoutHandler,
        PasswordResetHandler passwordResetHandler,
        SuperAdminLoginHandler superAdminLoginHandler,
        SwitchTenantHandler switchTenantHandler)
    {
        _registerHandler        = registerHandler;
        _loginHandler           = loginHandler;
        _refreshTokenHandler    = refreshTokenHandler;
        _logoutHandler          = logoutHandler;
        _passwordResetHandler   = passwordResetHandler;
        _superAdminLoginHandler = superAdminLoginHandler;
        _switchTenantHandler    = switchTenantHandler;
    }

    /// <summary>Registra un nuevo usuario en un tenant existente.</summary>
    /// <remarks>El tenant debe existir previamente. El email debe ser único por tenant.</remarks>
    /// <response code="200">Usuario creado. Retorna JWT listo para usar.</response>
    /// <response code="400">El tenant no existe o el email ya está registrado.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken ct)
    {
        var result = await _registerHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Inicia sesión y retorna un JWT Bearer.</summary>
    /// <remarks>
    /// El sistema solo requiere <b>email + password</b>.
    ///
    /// - Si el email corresponde al SuperAdmin (único) se emite un token global con <c>tenant_id = Guid.Empty</c>.
    /// - Si NO es SuperAdmin, el sistema resuelve automáticamente la empresa (tenant) a la que pertenece el usuario.
    ///
    /// El token incluye los claims: sub, email, tenant_id, full_name, role.
    /// </remarks>
    /// <response code="200">Login exitoso. Usar el campo `token` como Bearer en requests protegidos.</response>
    /// <response code="401">Credenciales incorrectas o usuario inactivo.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken ct)
    {
        var result = await _loginHandler.HandleAsync(command, ct);
        if (result.IsSuccess)
        {
            // Establecer refresh token en cookie httpOnly + incluirlo en body para clientes no-browser
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                SetRefreshCookie(result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        return MapAuthFailure(result.Error);
    }

    /// <summary>
    /// Recuperación de contraseña (modo Direct).
    /// Preparado para futuro: Email/Phone según configuración por empresa.
    /// </summary>
    /// <remarks>
    /// Este endpoint es anónimo. Solo permite reset directo si el tenant tiene PasswordResetMode=Direct.
    /// </remarks>
    [HttpPost("password-reset")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PasswordReset(
        [FromBody] PasswordResetCommand command,
        CancellationToken ct)
    {
        var result = await _passwordResetHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Login global de SuperAdmin: email + password (sin TenantId).
    /// Retorna un JWT con tenant_id = Guid.Empty; requiere selección de empresa posterior.
    /// </summary>
    [HttpPost("superadmin-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SuperAdminLogin([FromBody] SuperAdminLoginCommand command, CancellationToken ct)
    {
        var result = await _superAdminLoginHandler.HandleAsync(command, ct);
        if (result.IsSuccess)
            return this.ApiOk(result.Value);

        return MapAuthFailure(result.Error);
    }

    /// <summary>
    /// Renueva el access token usando un refresh token válido.
    /// El refresh token anterior queda revocado y se emite uno nuevo (rotación).
    /// </summary>
    /// <remarks>No requiere access token activo — llamar cuando el access token haya expirado.</remarks>
    /// <response code="200">Nuevo access token + refresh token emitidos.</response>
    /// <response code="401">Refresh token inválido, expirado o revocado.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest? request,
        CancellationToken ct)
    {
        // Cookie httpOnly tiene prioridad; si no hay, usa el body
        var rawToken = ResolveRefreshToken(request?.RefreshToken);
        if (string.IsNullOrWhiteSpace(rawToken))
            return this.ApiUnauthorized("Se requiere refresh token.");

        var result = await _refreshTokenHandler.HandleAsync(rawToken, ct);
        if (result.IsSuccess)
        {
            if (result.Value?.RefreshToken is not null && result.Value.RefreshTokenExpiry is not null)
                SetRefreshCookie(result.Value.RefreshToken, result.Value.RefreshTokenExpiry.Value);

            return this.ApiOk(result.Value);
        }

        return this.ApiUnauthorized("Refresh token inválido.");  // mensaje genérico — no exponer detalles
    }

    /// <summary>
    /// Cierra la sesión revocando el refresh token.
    /// No requiere access token vigente — el refresh token es la credencial de logout.
    /// </summary>
    /// <remarks>
    /// - <c>allDevices: false</c> (por defecto): revoca solo el refresh token del body.
    /// - <c>allDevices: true</c>: valida el refresh token para obtener el usuario y revoca todos sus tokens.
    /// </remarks>
    /// <response code="200">Sesión cerrada correctamente.</response>
    /// <response code="400">Refresh token no proporcionado o inválido.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken ct)
    {
        var rawToken = ResolveRefreshToken(request?.RefreshToken);
        var result   = await _logoutHandler.HandleAsync(
            rawToken ?? string.Empty,
            request?.AllDevices ?? false,
            ct);

        ClearRefreshCookie(); // siempre limpiar la cookie aunque el token ya no fuera válido
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>401 salvo error de panel SuperAdmin deshabilitado (403).</summary>
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

    /// <summary>
    /// Cambia el tenant activo para SuperAdmin emitiendo un nuevo JWT con tenant_id seleccionado.
    /// </summary>
    [HttpPost("switch-tenant")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantCommand command, CancellationToken ct)
    {
        var result = await _switchTenantHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}
