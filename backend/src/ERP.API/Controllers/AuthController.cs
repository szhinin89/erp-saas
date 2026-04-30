using Microsoft.AspNetCore.Mvc;
using ERP.Application.Auth.UseCases.Register;
using ERP.Application.Auth.UseCases.Login;
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

    public AuthController(RegisterHandler registerHandler, LoginHandler loginHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler    = loginHandler;
    }

    /// <summary>Registra un nuevo usuario en un tenant existente.</summary>
    /// <remarks>El tenant debe existir previamente. El email debe ser único por tenant.</remarks>
    /// <response code="200">Usuario creado. Retorna JWT listo para usar.</response>
    /// <response code="400">El tenant no existe o el email ya está registrado.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken ct)
    {
        var result = await _registerHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Login), result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Inicia sesión y retorna un JWT Bearer.</summary>
    /// <remarks>El token incluye los claims: sub, email, tenant_id, full_name, role.</remarks>
    /// <response code="200">Login exitoso. Usar el campo `token` como Bearer en requests protegidos.</response>
    /// <response code="401">Credenciales incorrectas o usuario inactivo.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken ct)
    {
        var result = await _loginHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { error = result.Error });
    }
}
