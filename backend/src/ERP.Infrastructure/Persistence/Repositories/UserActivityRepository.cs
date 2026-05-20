using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public class UserActivityRepository : IUserActivityRepository
{
    private readonly ErpDbContext _context;

    public UserActivityRepository(ErpDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(UserActivity activity, CancellationToken ct = default)
        => _context.UserActivities.AddAsync(activity, ct).AsTask();

    public async Task<IReadOnlyList<UserActivity>> GetMyRecentAsync(
        Guid subscriberId,
        Guid userId,
        string? module = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        if (take <= 0) return Array.Empty<UserActivity>();
        if (take > 200) take = 200;
        if (skip < 0) skip = 0;

        var q = _context.UserActivities
            .AsNoTracking()
            .Where(x => x.SubscriberId == subscriberId && x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(module))
        {
            var m = module.Trim();
            q = q.Where(x => x.Module == m);
        }

        return await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserActivity>> GetByEntityAsync(
        Guid subscriberId,
        string entityType,
        Guid entityId,
        int take = 10,
        CancellationToken ct = default)
    {
        if (entityId == Guid.Empty) return Array.Empty<UserActivity>();
        if (take <= 0) return Array.Empty<UserActivity>();
        if (take > 50) take = 50;

        var et = entityType.Trim();
        if (string.IsNullOrEmpty(et)) return Array.Empty<UserActivity>();

        return await _context.UserActivities
            .AsNoTracking()
            .Where(x => x.SubscriberId == subscriberId && x.EntityType == et && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }
}

