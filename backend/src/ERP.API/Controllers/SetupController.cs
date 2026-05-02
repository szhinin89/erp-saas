using ERP.API.Contracts;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.ClaimInitialSuperAdmin;
using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Rutas de instalación puntuales (sin autenticación previa).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class SetupController : ControllerBase
{
    private readonly ClaimInitialSuperAdminHandler _claimInitialSuperAdminHandler;

    public SetupController(ClaimInitialSuperAdminHandler claimInitialSuperAdminHandler) =>
        _claimInitialSuperAdminHandler = claimInitialSuperAdminHandler;

    /// <summary>
    /// Crea el primer (y único) SuperAdmin en la tabla <c>users</c> con <c>tenant_id</c> vacío (operador de plataforma),
    /// usando el token <c>Deployment:InitialSuperAdminSetupToken</c> (env <c>Deployment__InitialSuperAdminSetupToken</c>).
    /// No requiere empresas previas: el SuperAdmin crea tenants después (p. ej. panel <c>/superadmin</c> o <c>POST /api/tenants</c>).
    /// </summary>
    [HttpPost("superadmin")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClaimInitialSuperAdmin(
        [FromBody] ClaimInitialSuperAdminCommand command,
        CancellationToken ct)
    {
        var result = await _claimInitialSuperAdminHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<AuthResponseDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }
}
