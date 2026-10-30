using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.Sales.Services;

public sealed record SalesElectronicRecoveryCandidate(
    Guid TenantId, Guid CompanyId, Guid BranchId, Guid SourceEntityId,
    ElectronicDocumentType DocumentType = ElectronicDocumentType.Invoice);

public interface ISalesElectronicDocumentRecovery
{
    Task<IReadOnlyList<SalesElectronicRecoveryCandidate>> GetCandidatesAsync(int batchSize, CancellationToken ct);
    Task RecoverAsync(SalesElectronicRecoveryCandidate candidate, CancellationToken ct);
}
