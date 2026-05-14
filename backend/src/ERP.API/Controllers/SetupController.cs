using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.ClaimInitialSuperAdmin;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Rutas de instalación puntuales (sin autenticación previa).
/// </summary>
/// <remarks>
/// El token de <c>setupToken</c> lo emite <see cref="ERP.Application.Common.Interfaces.IFirstRunSetupService"/> (hash en BD, texto plano solo en consola al arrancar la API o en respuesta de <c>/api/dev/reset-first-run</c> en Development).
/// No usar <c>Deployment:InitialSuperAdminSetupToken</c> para este flujo: no participa en la validación del claim.
/// Ver <c>docs/SUPERADMIN-Y-FIRST-RUN.md</c>.
/// </remarks>
[ApiController]
[Modulo("Setup API", "perm:setup.api", "🧩", null, null, 986, VisibleEnMenu = false)]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class SetupController : ControllerBase
{
    private readonly ClaimInitialSuperAdminHandler _claimInitialSuperAdminHandler;
    private readonly IFirstRunSetupService _firstRunSetupService;
    private readonly IWebHostEnvironment _environment;

    public SetupController(
        ClaimInitialSuperAdminHandler claimInitialSuperAdminHandler,
        IFirstRunSetupService firstRunSetupService,
        IWebHostEnvironment environment)
    {
        _claimInitialSuperAdminHandler = claimInitialSuperAdminHandler;
        _firstRunSetupService = firstRunSetupService;
        _environment = environment;
    }

    /// <summary>
    /// Crea el primer (y único) SuperAdmin en la tabla <c>users</c> con <c>tenant_id</c> vacío (operador de plataforma),
    /// usando un token efímero de first-run emitido en la consola del servidor.
    /// No requiere empresas previas: el SuperAdmin crea tenants después (p. ej. panel <c>/superadmin</c> o <c>POST /api/tenants</c>).
    /// </summary>
    [HttpPost("superadmin")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ClaimInitialSuperAdmin(
        [FromBody] ClaimInitialSuperAdminCommand command,
        CancellationToken ct)
    {
        var result = await _claimInitialSuperAdminHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Alias de compatibilidad para scripts/automatizaciones previas.
    /// </summary>
    [HttpPost("claim-initial-superadmin")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ClaimInitialSuperAdminAlias(
        [FromBody] ClaimInitialSuperAdminCommand command,
        CancellationToken ct)
    {
        var result = await _claimInitialSuperAdminHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// SOLO DESARROLLO: reinicia first-run para pruebas automáticas y emite un token efímero nuevo.
    /// </summary>
    [HttpPost("/api/dev/reset-first-run")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetFirstRunForDevelopment(CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
            return this.ApiNotFound("Endpoint disponible solo en Development.");

        var result = await _firstRunSetupService.ResetForDevelopmentAsync(ct);
        return this.ApiOk(new
        {
            result.Message,
            result.RemovedSuperAdmins,
            result.SetupToken,
            result.ExpiresAtUtc
        });
    }
}
