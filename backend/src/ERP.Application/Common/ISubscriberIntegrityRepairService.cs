namespace ERP.Application.Common;

public sealed record EnterpriseIntegrityIssue(
    string Code,
    string Description,
    Guid? SubscriberId = null,
    Guid? CompanyId = null,
    Guid? IdentityUserId = null);

public sealed record EnterpriseIntegrityReport(
    IReadOnlyList<EnterpriseIntegrityIssue> Issues,
    int RepairedCount);

public interface ISubscriberIntegrityRepairService
{
    Task<EnterpriseIntegrityReport> ScanAsync(CancellationToken ct = default);
    Task<EnterpriseIntegrityReport> RepairAsync(CancellationToken ct = default);
}
