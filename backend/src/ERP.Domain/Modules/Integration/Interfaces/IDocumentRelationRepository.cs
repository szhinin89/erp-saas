using ERP.Domain.Modules.Integration.Entities;

namespace ERP.Domain.Modules.Integration.Interfaces;

public interface IDocumentRelationRepository
{
    Task AddAsync(DocumentRelation relation, CancellationToken ct = default);
    Task<bool> ExistsSourceRelationAsync(
        Guid subscriberId,
        string sourceModule,
        long sourceId,
        string relationType,
        CancellationToken ct = default);
    Task<long?> GetTargetIdBySourceAsync(
        Guid subscriberId,
        string sourceModule,
        long sourceId,
        string relationType,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
