using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Platform.Metrics;
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
    private readonly IMediator _mediator;
    private readonly ISubscriberRepository _subscribers;
    private readonly ISubscriberBillingRepository _billing;
    private readonly IConfiguration _config;

    public PlatformObservabilityController(
        IMediator mediator,
        ISubscriberRepository subscribers,
        ISubscriberBillingRepository billing,
        IConfiguration config)
    {
        _mediator = mediator;
        _subscribers = subscribers;
        _billing = billing;
        _config = config;
    }

    /// <summary>Dashboard operativo: tenants, lifecycle, billing risk.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var metricsResult = await _mediator.Send(new GetPlatformMetricsQuery(), ct);
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
}
