using MediatR;
using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.ClaimInitialPlatformOperator;
using ERP.Application.Auth.UseCases.PlatformOwnerSetup;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
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
    private readonly ISubscriberRepository _subscribers;
    private readonly IAccessRepository _access;

    public SetupController(
        IMediator mediator,
        IFirstRunSetupService firstRunSetupService,
        ISubscriberIntegrityRepairService integrityRepair,
        IWebHostEnvironment environment,
        ISubscriberRepository subscribers,
        IAccessRepository access)
    {
        _mediator             = mediator;
        _firstRunSetupService = firstRunSetupService;
        _integrityRepair      = integrityRepair;
        _environment          = environment;
        _subscribers          = subscribers;
        _access               = access;
    }

    // ── GET /api/setup/platform/status ────────────────────────────────────────

    /// <summary>
    /// Returns the first-run provisioning status of the platform.
    /// Called by the setup script before attempting provisioning.
    /// No authentication required.
    /// </summary>
    [HttpGet("platform/status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PlatformSetupStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlatformStatus(CancellationToken ct)
    {
        var allSubscribers   = await _subscribers.GetAllAsync(ct);
        var hasInternalOwner = allSubscribers.Any(s => s.IsInternalPlatformOwner);
        var hasPlatformOp    = await _access.AnyPrimaryPlatformOperatorAsync(ct);

        return this.ApiOk(new PlatformSetupStatusDto(
            IsFullyProvisioned:       hasInternalOwner && hasPlatformOp,
            HasInternalPlatformOwner: hasInternalOwner,
            HasPlatformOperator:      hasPlatformOp,
            Note: hasInternalOwner && hasPlatformOp
                ? "Platform is fully provisioned."
                : "Platform first-run provisioning required."));
    }

    // ── POST /api/setup/platform-owner ────────────────────────────────────────

    /// <summary>
    /// Creates the internal platform owner (Subscriber + Company) during first-run.
    /// Must be called before creating the platform operator user.
    /// Idempotent: safe to call multiple times.
    /// </summary>
    [HttpPost("platform-owner")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PlatformOwnerSetupResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetupPlatformOwner(
        [FromBody] PlatformOwnerSetupRequest body,
        CancellationToken ct)
    {
        var command = new PlatformOwnerSetupCommand(
            SetupToken:        body.SetupToken,
            PlatformName:      body.PlatformName,
            TaxId:             body.TaxId,
            LegalName:         body.LegalName,
            MainAddress:       body.MainAddress,
            Timezone:          body.Timezone,
            Email:             body.Email,
            TradeName:         body.TradeName,
            PreferredLanguage: body.PreferredLanguage ?? "es");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    // ── POST /api/setup/platform-operator ─────────────────────────────────────

    /// <summary>Crea el primer operador platform usando el token efímero de first-run.</summary>
    [HttpPost("platform-operator")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClaimInitialPlatformOperator(
        [FromBody] ClaimInitialPlatformOperatorCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("claim-initial-platform-operator")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public Task<IActionResult> ClaimInitialPlatformOperatorAlias(
        [FromBody] ClaimInitialPlatformOperatorCommand command, CancellationToken ct)
        => ClaimInitialPlatformOperator(command, ct);

    // ── Dev endpoints ─────────────────────────────────────────────────────────

    [HttpPost("/api/dev/reset-first-run")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
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

    [HttpPost("/api/dev/repair-enterprise-integrity")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> RepairEnterpriseIntegrity(
        [FromQuery] bool repair = false, CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment())
            return this.ApiNotFound("Endpoint disponible solo en Development.");

        var report = repair
            ? await _integrityRepair.RepairAsync(ct)
            : await _integrityRepair.ScanAsync(ct);

        return this.ApiOk(new { report.Issues, report.RepairedCount, mode = repair ? "repair" : "scan" });
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record PlatformSetupStatusDto(
    bool   IsFullyProvisioned,
    bool   HasInternalPlatformOwner,
    bool   HasPlatformOperator,
    string Note);

public sealed record PlatformOwnerSetupRequest(
    string  SetupToken,
    string  PlatformName,
    string  TaxId,
    string  LegalName,
    string  MainAddress,
    string  Timezone,
    string  Email,
    string? TradeName         = null,
    string? PreferredLanguage = "es");
