using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Auth.UseCases.Register;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Auth.UseCases.SuperAdminLogin;
using ERP.Application.Auth.UseCases.SwitchTenant;
using ERP.Application.Auth.DTOs;

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
    private readonly RegisterHandler _registerHandler;
    private readonly LoginHandler    _loginHandler;
    private readonly PasswordResetHandler _passwordResetHandler;
    private readonly SuperAdminLoginHandler _superAdminLoginHandler;
    private readonly SwitchTenantHandler _switchTenantHandler;

    public AuthController(
        RegisterHandler registerHandler,
        LoginHandler loginHandler,
        PasswordResetHandler passwordResetHandler,
        SuperAdminLoginHandler superAdminLoginHandler,
        SwitchTenantHandler switchTenantHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler    = loginHandler;
        _passwordResetHandler = passwordResetHandler;
        _superAdminLoginHandler = superAdminLoginHandler;
        _switchTenantHandler = switchTenantHandler;
    }

    /// <summary>Registra un nuevo usuario en un tenant existente.</summary>
    /// <remarks>El tenant debe existir previamente. El email debe ser único por tenant.</remarks>
    /// <response code="200">Usuario creado. Retorna JWT listo para usar.</response>
    /// <response code="400">El tenant no existe o el email ya está registrado.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken ct)
    {
        var result = await _registerHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<AuthResponseDto?>(
                Success: true,
                Message: "OK",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
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
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken ct)
    {
        var result = await _loginHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<AuthResponseDto?>(
                Success: true,
                Message: "OK",
                ResponseObject: result.Value))
            : Unauthorized(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Unauthorized",
                ResponseObject: new { }));
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
    public async Task<IActionResult> PasswordReset(
        [FromBody] PasswordResetCommand command,
        CancellationToken ct)
    {
        var result = await _passwordResetHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<object>(true, "OK", new { }))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    /// <summary>
    /// Login global de SuperAdmin: email + password (sin TenantId).
    /// Retorna un JWT con tenant_id = Guid.Empty; requiere selección de empresa posterior.
    /// </summary>
    [HttpPost("superadmin-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SuperAdminLogin([FromBody] SuperAdminLoginCommand command, CancellationToken ct)
    {
        var result = await _superAdminLoginHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<AuthResponseDto?>(true, "OK", result.Value))
            : Unauthorized(new ApiResponse<object>(false, result.Error ?? "Unauthorized", new { }));
    }

    /// <summary>
    /// Cambia el tenant activo para SuperAdmin emitiendo un nuevo JWT con tenant_id seleccionado.
    /// </summary>
    [HttpPost("switch-tenant")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantCommand command, CancellationToken ct)
    {
        var result = await _switchTenantHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<AuthResponseDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }
}
