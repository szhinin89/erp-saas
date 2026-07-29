using ERP.Application.MasterData.Reconciliation;

namespace ERP.API.Hangfire;

public interface IMasterDataReconciliationJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Job READ-ONLY de reconciliación legacy ↔ BusinessPartner.</summary>
public sealed partial class MasterDataReconciliationJob : IMasterDataReconciliationJob
{
    private readonly IMasterDataReconciliationService _reconciliation;
    private readonly ILogger<MasterDataReconciliationJob> _logger;

    public MasterDataReconciliationJob(
        IMasterDataReconciliationService reconciliation,
        ILogger<MasterDataReconciliationJob> logger
    )
    {
        _reconciliation = reconciliation;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var report = await _reconciliation.AnalyzeAsync(cancellationToken);
        if (report.IsHealthy)
        {
            LogNoIssues();
            return;
        }

        LogIssuesDetected(
            report.IssueCount,
            string.Join("; ", report.Issues.Take(5).Select(i => i.Code))
        );
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "MasterDataReconciliationJob: sin divergencias."
    )]
    private partial void LogNoIssues();

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "MasterDataReconciliationJob: {IssueCount} issue(s) detectadas. Critical sample: {Sample}"
    )]
    private partial void LogIssuesDetected(int issueCount, string sample);
}
