using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Platform.Metrics;
using ERP.Application.Platform.Observability;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using ERP.Domain.Billing.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>Platform Layer — observability helpers for control plane operators.</summary>
[ApiController]
[Route("api/platform/observability")]
[Authorize(Roles = PlatformAuthorizationRoles.Operators)]
[Tags("Platform")]
public sealed class PlatformObservabilityController : ControllerBase
{
    private readonly ILegacyEndpointUsageTracker _legacyUsage;
    private readonly ILegacyUsageTelemetryStore _telemetryStore;
    private readonly IMediator _mediator;
    private readonly ISubscriberRepository _subscribers;
    private readonly ISubscriberBillingRepository _billing;
    private readonly IConfiguration _config;

    public PlatformObservabilityController(
        ILegacyEndpointUsageTracker legacyUsage,
        ILegacyUsageTelemetryStore telemetryStore,
        IMediator mediator,
        ISubscriberRepository subscribers,
        ISubscriberBillingRepository billing,
        IConfiguration config)
    {
        _legacyUsage = legacyUsage;
        _telemetryStore = telemetryStore;
        _mediator = mediator;
        _subscribers = subscribers;
        _billing = billing;
        _config = config;
    }

    /// <summary>Dashboard de uso legacy (endpoints, UI, masterdata) — PostgreSQL persistente.</summary>
    [HttpGet("legacy-endpoints")]
    [ProducesResponseType(typeof(ApiResponse<LegacyStranglerDashboard>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LegacyEndpoints(
        [FromQuery] int top = 50,
        [FromQuery] int recent = 100,
        CancellationToken ct = default)
        => this.ApiOk(await _telemetryStore.GetDashboardAsync(top, recent, ct));

    /// <summary>Beacon desde UI legacy (rutas redirect, páginas coexistencia).</summary>
    [HttpPost("legacy-ui")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status204NoContent)]
    public IActionResult RecordLegacyUi([FromBody] LegacyUiBeaconRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.RouteKey))
            return this.ApiBadRequest("routeKey requerido.");
        _legacyUsage.RecordUiRoute(body.RouteKey, body.Referrer);
        return NoContent();
    }

    /// <summary>Beacon masterdata fallback (facade → legacy customers/suppliers).</summary>
    [HttpPost("legacy-masterdata")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult RecordLegacyMasterData([FromBody] LegacyMasterDataBeaconRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.SourceKey))
            return this.ApiBadRequest("sourceKey requerido.");
        _legacyUsage.RecordMasterData(body.SourceKey, body.Detail);
        return NoContent();
    }

    /// <summary>Dashboard operativo: tenants, lifecycle, billing risk, strangler migration.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var metricsResult = await _mediator.Send(new GetPlatformMetricsQuery(), ct);
        var strangler = await _telemetryStore.GetDashboardAsync(10, 20, ct);
        var all = await _subscribers.GetAllAsync(ct);

        var overdue = 0;
        var pastDueAccounts = 0;
        foreach (var s in all)
        {
            var account = await _billing.GetAccountBySubscriberIdAsync(s.Id, ct);
            if (account is null) continue;
            if (account.Status == BillingAccountStatus.PastDue)
            {
                pastDueAccounts++;
                overdue++;
            }
        }

        var activeTenants = all.Count(s =>
            s.LifecycleStatus is SubscriberLifecycleStatus.Active or SubscriberLifecycleStatus.Trial);

        return this.ApiOk(new
        {
            activeTenants,
            suspendedTenants = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Suspended),
            graceTenants = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.GracePeriod),
            overdueAccounts = pastDueAccounts,
            legacyMigrationPercent = strangler.MigrationCompletePercent,
            legacyTotalHits = strangler.TotalHits,
            metrics = metricsResult.IsSuccess ? metricsResult.Value : null,
            prometheus = _config.GetValue("Observability:EnablePrometheus", true) ? "/metrics" : null,
            healthChecks = new[]
            {
                "/health/live",
                "/health/ready",
                "/health/security-context",
            },
            note = "MRR/churn requieren integración payment provider (Phase 3 backlog).",
        });
    }

    /// <summary>Índice de health checks y métricas expuestas por el host API.</summary>
    [HttpGet("health-index")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult HealthIndex()
    {
        var prometheusEnabled = _config.GetValue("Observability:EnablePrometheus", true);
        return this.ApiOk(new
        {
            healthChecks = new[]
            {
                "/health/live",
                "/health/ready",
                "/health/security-context",
                "/health/membership-consistency",
                "/health/masterdata-sync",
                "/health/background-context",
                "/health/query-filter-enforcement",
                "/health/masterdata-reconciliation",
            },
            prometheus = prometheusEnabled ? "/metrics" : null,
            correlationHeader = "X-Correlation-Id",
        });
    }

    public sealed record LegacyUiBeaconRequest(string RouteKey, string? Referrer);

    public sealed record LegacyMasterDataBeaconRequest(string SourceKey, string? Detail);
}
