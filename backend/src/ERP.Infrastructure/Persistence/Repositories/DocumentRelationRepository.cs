using ERP.Domain.Modules.Integration.Entities;
using ERP.Domain.Modules.Integration.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class DocumentRelationRepository : IDocumentRelationRepository
{
    private readonly ErpDbContext _db;

    public DocumentRelationRepository(ErpDbContext db) => _db = db;

    public async Task AddAsync(DocumentRelation relation, CancellationToken ct = default)
        => await _db.DocumentRelations.AddAsync(relation, ct);

    public Task<bool> ExistsSourceRelationAsync(
        Guid subscriberId,
        string sourceModule,
        long sourceId,
        string relationType,
        CancellationToken ct = default)
        => _db.DocumentRelations
            .AsNoTracking()
            .AnyAsync(r =>
                r.SubscriberId == subscriberId
                && r.SourceModule == sourceModule
                && r.SourceId == sourceId
                && r.RelationType == relationType,
                ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<long?> GetTargetIdBySourceAsync(
        Guid subscriberId,
        string sourceModule,
        long sourceId,
        string relationType,
        CancellationToken ct = default)
        => _db.DocumentRelations
            .AsNoTracking()
            .Where(r =>
                r.SubscriberId == subscriberId
                && r.SourceModule == sourceModule
                && r.SourceId == sourceId
                && r.RelationType == relationType)
            .Select(r => r.TargetId)
            .FirstOrDefaultAsync(ct);
}
