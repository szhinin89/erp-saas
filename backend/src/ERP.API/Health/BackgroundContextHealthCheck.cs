using ERP.Application.Common.Security;
using ERP.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

/// <summary>/health/background-context — AsyncLocal de jobs no debe quedar sucio fuera de ejecución.</summary>
public sealed class BackgroundContextHealthCheck : IHealthCheck
{
    private readonly ISecurityMetrics _metrics;

    public BackgroundContextHealthCheck(ISecurityMetrics metrics) => _metrics = metrics;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (JobTenantContext.Current != Guid.Empty || JobCompanyContext.Current != Guid.Empty)
        {
            _metrics.RecordBackgroundContextLeakDetected();
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Job AsyncLocal context leak: tenant or company set outside job execution."
                )
            );
        }

        return Task.FromResult(HealthCheckResult.Healthy("Background context clean."));
    }
}
