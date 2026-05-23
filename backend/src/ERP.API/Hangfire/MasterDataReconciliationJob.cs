using ERP.Application.MasterData.Reconciliation;

namespace ERP.API.Hangfire;

public interface IMasterDataReconciliationJob
{
    Task ExecuteAsync(CancellationToken ct = default);
}

/// <summary>Job READ-ONLY de reconciliación legacy ↔ BusinessPartner.</summary>
public sealed class MasterDataReconciliationJob : IMasterDataReconciliationJob
{
    private readonly IMasterDataReconciliationService _reconciliation;
    private readonly ILogger<MasterDataReconciliationJob> _logger;

    public MasterDataReconciliationJob(
        IMasterDataReconciliationService reconciliation,
        ILogger<MasterDataReconciliationJob> logger)
    {
        _reconciliation = reconciliation;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var report = await _reconciliation.AnalyzeAsync(ct);
        if (report.IsHealthy)
        {
            _logger.LogInformation("MasterDataReconciliationJob: sin divergencias.");
            return;
        }

        _logger.LogWarning(
            "MasterDataReconciliationJob: {IssueCount} issue(s) detectadas. Critical sample: {Sample}",
            report.IssueCount,
            string.Join("; ", report.Issues.Take(5).Select(i => i.Code)));
    }
}
