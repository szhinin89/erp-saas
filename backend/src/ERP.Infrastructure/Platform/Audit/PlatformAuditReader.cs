using ERP.Application.Platform.Audit;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Platform.Audit;

public sealed class PlatformAuditReader : IPlatformAuditReader
{
    private readonly ErpDbContext _db;

    public PlatformAuditReader(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<PlatformAuditLogListItem>> ListRecentAsync(
        int limit,
        Guid? targetSubscriberId = null,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, 500);
        var query = _db.PlatformAuditLogs.AsNoTracking().AsQueryable();

        if (targetSubscriberId is { } sid)
            query = query.Where(x => x.TargetSubscriberId == sid);

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new PlatformAuditLogListItem(
                x.Id,
                x.ActorUserId,
                x.ActorEmail,
                x.Action,
                x.TargetSubscriberId,
                x.ResourceType,
                x.ResourceId,
                x.Notes,
                x.CreatedAtUtc))
            .ToListAsync(ct);
    }
}
