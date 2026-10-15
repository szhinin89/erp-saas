using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ERP.Infrastructure.BackgroundProcessing;

/// <summary>Durable platform checkpoints. The runner owns exclusion for each stream's job.</summary>
public static class BackgroundJobProgress
{
    public sealed record Position(Guid Id, DateTime CreatedAt);
    public static async Task<Position?> ReadAsync(ErpDbContext db, string stream, CancellationToken ct) =>
        await db.Database.SqlQuery<Position>($"SELECT candidate_id AS \"Id\", candidate_created_at AS \"CreatedAt\" FROM background_job_progress WHERE stream = {stream}")
            .SingleOrDefaultAsync(ct);

    public static Task WriteAsync(ErpDbContext db, string stream, Guid id, DateTime createdAt, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO background_job_progress (stream, candidate_id, candidate_created_at)
            VALUES ({stream}, {id}, {createdAt})
            ON CONFLICT (stream) DO UPDATE SET candidate_id = EXCLUDED.candidate_id,
                candidate_created_at = EXCLUDED.candidate_created_at
            """, ct);

    public static async Task<IReadOnlyList<TCandidate>> ReadBatchAsync<TEntity, TCandidate>(
        ErpDbContext db, string stream, IQueryable<TEntity> query,
        Expression<Func<TEntity, TCandidate>> projection, Func<TCandidate, Guid> candidateId,
        Expression<Func<TEntity, DateTime?>> age, int limit, CancellationToken ct) where TEntity : class
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var position = await ReadAsync(db, stream, ct);
        var last = position?.Id ?? Guid.Empty;
        JobMetrics.RecordBacklog(await query.LongCountAsync(ct), await query.MinAsync(age, ct), stream);
        // UUID ordering is stable in PostgreSQL. Rotate after the last discovery, including
        // failed candidates, so a poison item cannot monopolize every subsequent batch.
        var rows = await query.OrderBy(row => EF.Property<Guid>(row, "Id").CompareTo(last) <= 0)
            .ThenBy(row => EF.Property<Guid>(row, "Id"))
            .Take(limit).Select(projection).ToListAsync(ct);
        if (rows.Count > 0)
            await WriteAsync(db, stream, candidateId(rows[^1]), DateTime.UnixEpoch, ct);
        return rows;
    }
}
