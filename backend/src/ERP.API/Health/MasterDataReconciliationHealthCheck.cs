using ERP.Application.MasterData.Reconciliation;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

/// <summary>/health/masterdata-reconciliation — drift legacy ↔ BusinessPartner (READ-ONLY).</summary>
public sealed class MasterDataReconciliationHealthCheck : IHealthCheck
{
    private readonly IMasterDataReconciliationService _reconciliation;

    public MasterDataReconciliationHealthCheck(IMasterDataReconciliationService reconciliation) =>
        _reconciliation = reconciliation;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var report = await _reconciliation.AnalyzeAsync(cancellationToken);

        if (report.IsHealthy)
            return HealthCheckResult.Healthy("MasterData reconciliation: sin issues.");

        var critical = report.Issues.Count(i => i.Severity == "critical");
        var data = new Dictionary<string, object>
        {
            ["issueCount"] = report.IssueCount,
            ["criticalCount"] = critical,
            ["sample"] = report.Issues.Take(5).Select(i => $"{i.Code}: {i.Message}").ToArray(),
        };

        return critical > 0
            ? HealthCheckResult.Unhealthy(
                "MasterData reconciliation: issues críticas detectadas.",
                data: data
            )
            : HealthCheckResult.Degraded("MasterData reconciliation: drift detectado.", data: data);
    }
}
