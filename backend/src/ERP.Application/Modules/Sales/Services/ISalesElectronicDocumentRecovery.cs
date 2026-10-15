namespace ERP.Application.Modules.Sales.Services;

public sealed record SalesElectronicRecoveryCandidate(
    Guid TenantId, Guid CompanyId, Guid BranchId, Guid InvoiceId);

public interface ISalesElectronicDocumentRecovery
{
    Task<IReadOnlyList<SalesElectronicRecoveryCandidate>> GetCandidatesAsync(int batchSize, CancellationToken ct);
    Task RecoverAsync(SalesElectronicRecoveryCandidate candidate, CancellationToken ct);
}
