using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Audit;

/// <summary>
/// Única implementación de <see cref="IAuditReader{TAudit}"/>, reutilizada por todos los
/// dominios vía registro open-generic en DI — ver <c>DependencyInjection.cs</c>.
/// </summary>
public sealed class EfAuditReader<TAudit> : IAuditReader<TAudit> where TAudit : AuditRecordBase
{
    private readonly ErpDbContext _db;

    public EfAuditReader(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<TAudit>> GetByEntityAsync(
        Guid tenantId, Guid entityId, int take = 20, CancellationToken ct = default)
    {
        if (take <= 0) return Array.Empty<TAudit>();
        if (take > 200) take = 200;

        return await _db.Set<TAudit>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EntityId == entityId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, TAudit>> GetLastByEntityIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> entityIds, CancellationToken ct = default)
    {
        if (entityIds.Count == 0) return new Dictionary<Guid, TAudit>();
        var ids = entityIds.Distinct().ToArray();

        var rows = await _db.Set<TAudit>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.EntityId))
            .GroupBy(x => x.EntityId)
            .Select(g => g.OrderByDescending(x => x.OccurredAtUtc).First())
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.EntityId);
    }

    public async Task<IReadOnlyList<TAudit>> GetByUserAsync(
        Guid tenantId, Guid userId, DateTime? fromUtc = null, DateTime? toUtc = null,
        int take = 50, CancellationToken ct = default)
    {
        if (take <= 0) return Array.Empty<TAudit>();
        if (take > 200) take = 200;

        var q = _db.Set<TAudit>().AsNoTracking().Where(x => x.TenantId == tenantId && x.UserId == userId);
        if (fromUtc.HasValue) q = q.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(x => x.OccurredAtUtc <= toUtc.Value);

        return await q.OrderByDescending(x => x.OccurredAtUtc).Take(take).ToListAsync(ct);
    }
}
