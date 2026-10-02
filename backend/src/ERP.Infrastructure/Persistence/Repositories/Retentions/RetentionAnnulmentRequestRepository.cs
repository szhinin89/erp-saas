using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Retentions;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — fail-closed por tenant y empresa explícitos (además de los filtros
/// globales). Las únicas consultas cross-tenant son <see cref="GetPendingFinalizationAsync"/> y
/// <see cref="GetDueForSriVerificationAsync"/> (job de recuperación, vía el accessor sancionado
/// <c>AsPlatformQuery</c>, solo identificadores).
/// </summary>
public sealed class RetentionAnnulmentRequestRepository : IRetentionAnnulmentRequestRepository
{
    private readonly ErpDbContext _db;

    public RetentionAnnulmentRequestRepository(ErpDbContext db) => _db = db;

    public Task AddAsync(RetentionAnnulmentRequest request, CancellationToken ct = default) =>
        _db.RetentionAnnulmentRequests.AddAsync(request, ct).AsTask();

    public async Task<RetentionAnnulmentRequest?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    )
    {
        var request = await _db.RetentionAnnulmentRequests.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id,
            ct
        );
        if (request is not null)
            await _db.Entry(request).ReloadAsync(ct);
        return request;
    }

    public Task<RetentionAnnulmentRequest?> GetOpenByRetentionAsync(
        Guid tenantId,
        Guid companyId,
        Guid retentionDocumentId,
        CancellationToken ct = default
    ) =>
        _db.RetentionAnnulmentRequests.FirstOrDefaultAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.RetentionDocumentId == retentionDocumentId
                && (
                    x.Status == RetentionAnnulmentStatus.PendingSubmission
                    || x.Status == RetentionAnnulmentStatus.PendingSriResolution
                ),
            ct
        );

    public Task<RetentionAnnulmentRequest?> GetLatestByRetentionAsync(
        Guid tenantId,
        Guid companyId,
        Guid retentionDocumentId,
        CancellationToken ct = default
    ) =>
        _db.RetentionAnnulmentRequests.AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.RetentionDocumentId == retentionDocumentId
            )
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<(Guid TenantId, Guid CompanyId, Guid RequestId)>> GetPendingFinalizationAsync(
        int take,
        CancellationToken ct = default
    )
    {
        var rows = await _db.RetentionAnnulmentRequests
            .AsPlatformQuery()
            .AsNoTracking()
            .Where(x => x.Status == RetentionAnnulmentStatus.Accepted && x.FinalizedAtUtc == null)
            .OrderBy(x => x.ResolvedAtUtc)
            .Take(take)
            .Select(x => new { x.TenantId, x.CompanyId, x.Id })
            .ToListAsync(ct);
        return rows.Select(row => (row.TenantId, row.CompanyId, row.Id)).ToList();
    }

    public async Task<IReadOnlyList<(Guid TenantId, Guid CompanyId, Guid RequestId)>> GetDueForSriVerificationAsync(
        DateTime checkedBeforeUtc,
        int take,
        CancellationToken ct = default
    )
    {
        var rows = await _db.RetentionAnnulmentRequests
            .AsPlatformQuery()
            .AsNoTracking()
            .Where(x =>
                x.Status == RetentionAnnulmentStatus.PendingSriResolution
                && (x.LastSriCheckAtUtc == null || x.LastSriCheckAtUtc < checkedBeforeUtc)
            )
            .OrderBy(x => x.LastSriCheckAtUtc ?? x.SubmittedAtUtc)
            .Take(take)
            .Select(x => new { x.TenantId, x.CompanyId, x.Id })
            .ToListAsync(ct);
        return rows.Select(row => (row.TenantId, row.CompanyId, row.Id)).ToList();
    }
}
