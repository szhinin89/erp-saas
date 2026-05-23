using MediatR;
using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.ClaimInitialPlatformOperator;
using ERP.Application.Common;
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
    private readonly ISubscriberIntegrityRepairService _integrityRepair;
    private readonly IWebHostEnvironment _environment;

    public SetupController(
        IMediator mediator,
        IFirstRunSetupService firstRunSetupService,
        ISubscriberIntegrityRepairService integrityRepair,
        IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _firstRunSetupService = firstRunSetupService;
        _integrityRepair = integrityRepair;
        _environment = environment;
    }

    /// <summary>Crea el primer operador platform usando el token efímero de first-run.</summary>
    [HttpPost("platform-operator")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ClaimInitialPlatformOperator(
        [FromBody] ClaimInitialPlatformOperatorCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Alias estable para scripts/automatizaciones.</summary>
    [HttpPost("claim-initial-platform-operator")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ClaimInitialPlatformOperatorAlias(
        [FromBody] ClaimInitialPlatformOperatorCommand command,
        CancellationToken ct)
        => ClaimInitialPlatformOperator(command, ct);

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
            result.RemovedPlatformOperators,
            result.SetupToken,
            result.ExpiresAtUtc
        });
    }

    /// <summary>SOLO DESARROLLO: escanea y repara integridad enterprise (subscriber/company/billing).</summary>
    [HttpPost("/api/dev/repair-enterprise-integrity")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RepairEnterpriseIntegrity(
        [FromQuery] bool repair = false,
        CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment())
            return this.ApiNotFound("Endpoint disponible solo en Development.");

        var report = repair
            ? await _integrityRepair.RepairAsync(ct)
            : await _integrityRepair.ScanAsync(ct);

        return this.ApiOk(new
        {
            report.Issues,
            report.RepairedCount,
            mode = repair ? "repair" : "scan",
        });
    }
}
