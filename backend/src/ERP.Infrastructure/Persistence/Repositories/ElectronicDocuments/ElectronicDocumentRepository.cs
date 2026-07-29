using ERP.Application.Common.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;

public sealed class ElectronicDocumentRepository : IElectronicDocumentRepository
{
    private readonly ErpDbContext _db;
    private readonly ICompanyClock _companyClock;

    public ElectronicDocumentRepository(ErpDbContext db, ICompanyClock companyClock)
    {
        _db = db;
        _companyClock = companyClock;
    }

    public Task<ElectronicDocument?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) => _db.ElectronicDocuments.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public Task<ElectronicDocument?> GetBySourceAsync(
        Guid tenantId,
        string sourceModule,
        Guid sourceEntityId,
        CancellationToken ct = default
    ) =>
        _db.ElectronicDocuments.FirstOrDefaultAsync(
            x =>
                x.TenantId == tenantId
                && x.SourceModule == sourceModule
                && x.SourceEntityId == sourceEntityId,
            ct
        );

    public Task AddAsync(ElectronicDocument document, CancellationToken ct = default) =>
        _db.ElectronicDocuments.AddAsync(document, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private IQueryable<ElectronicDocument> Scoped(Guid tenantId, Guid? companyId)
    {
        var query = _db.ElectronicDocuments.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (companyId.HasValue)
            query = query.Where(x => x.CompanyId == companyId.Value);
        return query;
    }

    public async Task<(IReadOnlyList<ElectronicDocument> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid? companyId,
        DateTime? dateFromUtc,
        DateTime? dateToUtc,
        IReadOnlyList<ElectronicDocumentState>? states,
        ElectronicDocumentType? documentType,
        string? environment,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = Scoped(tenantId, companyId);

        if (dateFromUtc.HasValue)
            query = query.Where(x => x.CreatedAt >= dateFromUtc.Value);
        if (dateToUtc.HasValue)
            query = query.Where(x => x.CreatedAt <= dateToUtc.Value);
        if (states is { Count: > 0 })
            query = query.Where(x => states.Contains(x.CurrentState));
        if (documentType.HasValue)
            query = query.Where(x => x.DocumentType == documentType.Value);
        if (!string.IsNullOrWhiteSpace(environment))
            query = query.Where(x => x.Environment == environment);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                (x.AccessKey != null && x.AccessKey.Value.Contains(s))
                || (x.AuthorizationNumber != null && x.AuthorizationNumber.Value.Contains(s))
                || (
                    x.SourceModule == "Sales"
                    && _db.SalesInvoices.Any(si =>
                        si.Id == x.SourceEntityId
                        && (si.InvoiceNumber.Contains(s) || si.Customer.Name.Contains(s))
                    )
                )
            );
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyDictionary<ElectronicDocumentState, int>> GetStateCountsAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    )
    {
        var counts = await Scoped(tenantId, companyId)
            .GroupBy(x => x.CurrentState)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => x.State, x => x.Count);
    }

    public async Task<int> CountAuthorizedTodayAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    )
    {
        var (startUtc, endUtc) = await GetCompanyTodayRangeUtcAsync(tenantId, companyId, ct);
        return await Scoped(tenantId, companyId)
            .Where(x =>
                x.CurrentState == ElectronicDocumentState.Authorized
                && x.AuthorizationDate != null
                && x.AuthorizationDate >= startUtc
                && x.AuthorizationDate < endUtc
            )
            .CountAsync(ct);
    }

    public async Task<int> CountErrorsTodayAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    )
    {
        var (startUtc, endUtc) = await GetCompanyTodayRangeUtcAsync(tenantId, companyId, ct);
        return await Scoped(tenantId, companyId)
            .Where(x =>
                (
                    x.CurrentState == ElectronicDocumentState.Rejected
                    || x.CurrentState == ElectronicDocumentState.DeadLetter
                    || x.CurrentState == ElectronicDocumentState.Failed
                )
                && x.UpdatedAt != null
                && x.UpdatedAt >= startUtc
                && x.UpdatedAt < endUtc
            )
            .CountAsync(ct);
    }

    public async Task<double?> GetAverageAuthorizationMinutesAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    )
    {
        var authorized = await Scoped(tenantId, companyId)
            .Where(x => x.AuthorizationDate != null)
            .Select(x => new { x.CreatedAt, AuthorizationDate = x.AuthorizationDate!.Value })
            .ToListAsync(ct);

        if (authorized.Count == 0)
            return null;
        return authorized.Average(x => (x.AuthorizationDate - x.CreatedAt).TotalMinutes);
    }

    public Task<int> CountPendingRetriesAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    ) => Scoped(tenantId, companyId).Where(x => x.RetryCount > 0).CountAsync(ct);

    public async Task<int> CountCreatedTodayAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    )
    {
        var (startUtc, endUtc) = await GetCompanyTodayRangeUtcAsync(tenantId, companyId, ct);
        return await Scoped(tenantId, companyId)
            .Where(x => x.CreatedAt >= startUtc && x.CreatedAt < endUtc)
            .CountAsync(ct);
    }

    private Task<(DateTime StartUtc, DateTime EndUtc)> GetCompanyTodayRangeUtcAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct
    )
    {
        if (companyId is not Guid currentCompanyId || currentCompanyId == Guid.Empty)
            throw new InvalidOperationException(
                "El dashboard de documentos electrónicos requiere contexto de empresa para calcular 'Hoy'."
            );

        return _companyClock.TodayUtcRangeAsync(currentCompanyId, tenantId, ct);
    }

    public async Task<IReadOnlyList<ElectronicDocument>> GetRetryCandidatesAsync(
        CancellationToken ct = default
    ) =>
        await _db
            .ElectronicDocuments.Where(x =>
                x.CurrentState == ElectronicDocumentState.Signed
                || x.CurrentState == ElectronicDocumentState.Received
                || x.CurrentState == ElectronicDocumentState.Draft
                || x.CurrentState == ElectronicDocumentState.Failed
            )
            .ToListAsync(ct);
}
