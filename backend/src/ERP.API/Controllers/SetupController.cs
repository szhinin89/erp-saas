using MediatR;
using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.ClaimInitialSuperAdmin;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Rutas de instalación puntuales (sin autenticación previa).</summary>
[ApiController]
[AppFeature("Setup API", "perm:setup.api", "🧩", null, null, 986, IsVisibleInMenu = false)]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class SetupController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFirstRunSetupService _firstRunSetupService;
    private readonly IWebHostEnvironment _environment;

    public SetupController(
        IMediator mediator,
        IFirstRunSetupService firstRunSetupService,
        IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _firstRunSetupService = firstRunSetupService;
        _environment = environment;
    }

    /// <summary>Crea el primer SuperAdmin usando el token efímero de first-run.</summary>
    [HttpPost("superadmin")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ClaimInitialSuperAdmin(
        [FromBody] ClaimInitialSuperAdminCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Alias de compatibilidad para scripts/automatizaciones previas.</summary>
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
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>SOLO DESARROLLO: reinicia first-run para pruebas automáticas.</summary>
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
