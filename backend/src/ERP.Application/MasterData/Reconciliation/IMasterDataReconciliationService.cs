namespace ERP.Application.MasterData.Reconciliation;

/// <summary>
/// Detección READ-ONLY de divergencias legacy ↔ BusinessPartner (sin autocorrección).
/// </summary>
public interface IMasterDataReconciliationService
{
    Task<MasterDataReconciliationReport> AnalyzeAsync(CancellationToken ct = default);
}

public sealed record MasterDataReconciliationIssue(
    string Code,
    string Severity,
    string Message,
    Guid? SubscriberId = null,
    Guid? EntityId = null);

public sealed record MasterDataReconciliationReport(
    bool IsHealthy,
    int IssueCount,
    IReadOnlyList<MasterDataReconciliationIssue> Issues);
