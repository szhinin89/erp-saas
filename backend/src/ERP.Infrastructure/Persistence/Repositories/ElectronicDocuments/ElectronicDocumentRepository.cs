using ERP.Infrastructure.BackgroundProcessing;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Configuration.Constants;
using ERP.Infrastructure.Services;
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

    public Task<DateTime?> GetFirstReceivedAtUtcAsync(
        Guid tenantId,
        Guid id,
        DateTime? sinceUtc = null,
        CancellationToken ct = default
    ) =>
        Since(
                _db.Set<ElectronicDocumentAudit>()
                    .Where(a =>
                        a.TenantId == tenantId
                        && a.EntityId == id
                        && a.ToState == ElectronicDocumentState.Received
                    ),
                sinceUtc
            )
            .MinAsync(a => (DateTime?)a.OccurredAtUtc, ct);

    public async Task<ElectronicDocumentRejectionEvidence?> GetRejectionEvidenceAsync(
        Guid tenantId,
        Guid id,
        DateTime? sinceUtc,
        CancellationToken ct = default
    )
    {
        var rejection = await Since(
                _db.Set<ElectronicDocumentAudit>()
                    .AsNoTracking()
                    .Where(a =>
                        a.TenantId == tenantId
                        && a.EntityId == id
                        && a.ToState == ElectronicDocumentState.Rejected
                    ),
                sinceUtc
            )
            .OrderByDescending(a => a.OccurredAtUtc)
            .Select(a => new { a.FromState, a.OccurredAtUtc })
            .FirstOrDefaultAsync(ct);
        if (rejection is null)
            return null;

        // Un intento termina en a lo sumo un rechazo (Rejected es terminal dentro del intento): los
        // mensajes registrados desde el inicio del intento son los de ese rechazo.
        var messages = await Since(
                _db.Set<ElectronicDocumentSriMessage>()
                    .AsNoTracking()
                    .Where(m => m.TenantId == tenantId && m.EntityId == id),
                sinceUtc
            )
            .OrderBy(m => m.OccurredAtUtc)
            .Select(m => new SriMessage(m.Code, m.MessageType, m.Message, m.AdditionalInfo))
            .ToListAsync(ct);

        return new ElectronicDocumentRejectionEvidence(
            rejection.FromState,
            rejection.OccurredAtUtc,
            messages
        );
    }

    private static IQueryable<T> Since<T>(IQueryable<T> records, DateTime? sinceUtc)
        where T : ERP.Domain.Audit.AuditRecordBase =>
        sinceUtc is null ? records : records.Where(a => a.OccurredAtUtc >= sinceUtc.Value);

    public Task<ElectronicDocumentTransmissionAttempt?> GetTransmissionAttemptByIdempotencyKeyAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        Guid idempotencyKey,
        CancellationToken ct = default
    ) =>
        _db.ElectronicDocumentTransmissionAttempts.AsNoTracking()
            .FirstOrDefaultAsync(
                a =>
                    a.TenantId == tenantId
                    && a.ElectronicDocumentId == electronicDocumentId
                    && a.IdempotencyKey == idempotencyKey,
                ct
            );

    public Task<ElectronicDocumentTransmissionAttempt?> GetLatestTransmissionAttemptAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        CancellationToken ct = default
    ) =>
        _db.ElectronicDocumentTransmissionAttempts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.ElectronicDocumentId == electronicDocumentId)
            .OrderByDescending(a => a.AttemptNumber)
            .FirstOrDefaultAsync(ct);

    public Task AddTransmissionAttemptAsync(
        ElectronicDocumentTransmissionAttempt attempt,
        CancellationToken ct = default
    ) => _db.ElectronicDocumentTransmissionAttempts.AddAsync(attempt, ct).AsTask();

    public void DiscardTransmissionAttempt(ElectronicDocumentTransmissionAttempt attempt) =>
        _db.Entry(attempt).State = EntityState.Detached;

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

    public async Task<IReadOnlyDictionary<Guid, ElectronicDocumentState>> GetCurrentStatesBySourcesAsync(
        Guid tenantId,
        string sourceModule,
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken ct = default
    )
    {
        if (sourceEntityIds.Count == 0)
            return new Dictionary<Guid, ElectronicDocumentState>();

        var ids = sourceEntityIds.Distinct().ToList();
        var rows = await _db.ElectronicDocuments
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.SourceModule == sourceModule
                && ids.Contains(x.SourceEntityId)
            )
            .Select(x => new { x.SourceEntityId, x.CurrentState })
            .ToListAsync(ct);

        // Un origen tiene a lo sumo un documento electrónico (mismo contrato que GetBySourceAsync);
        // el agrupamiento solo evita una excepción si existiera un duplicado histórico.
        return rows
            .GroupBy(x => x.SourceEntityId)
            .ToDictionary(g => g.Key, g => g.First().CurrentState);
    }

    public Task AddAsync(ElectronicDocument document, CancellationToken ct = default) =>
        _db.ElectronicDocuments.AddAsync(document, ct).AsTask();

    public Task ReloadAsync(ElectronicDocument document, CancellationToken ct = default) =>
        _db.Entry(document).ReloadAsync(ct);

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
        DateTime? dateToUtcExclusive,
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
        if (dateToUtcExclusive.HasValue)
            query = query.Where(x => x.CreatedAt < dateToUtcExclusive.Value);
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

    private IQueryable<ERP.Domain.Configuration.Entities.OrgSetting> RetryMaximumSettings() =>
        _db.OrgSettings.AsPlatformQuery().Where(s => s.Scope == ERP.Domain.Configuration.Enums.OrgScope.Company
            && s.ScopeId == s.CompanyId && s.Key == OrgSettingKeys.ElectronicDocuments.MaxRetryAttempts);

    public Task<string?> GetRetryMaximumSettingAsync(Guid tenantId, Guid companyId, CancellationToken ct = default) =>
        RetryMaximumSettings().Where(s => s.TenantId == tenantId && s.CompanyId == companyId)
            .Select(s => s.Value).FirstOrDefaultAsync(ct);

    private IQueryable<ElectronicDocument> WithinRetryMaximum(IQueryable<ElectronicDocument> documents)
    {
        var settings = RetryMaximumSettings();
        return documents.Where(ElectronicDocumentRetryPolicy.WithinAutomaticBudget(x => settings
            .Where(s => s.TenantId == x.TenantId && s.CompanyId == x.CompanyId)
            .Select(s => s.Value).FirstOrDefault()));
    }

    public Task<int> CountPendingRetriesAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    ) =>
        WithinRetryMaximum(Scoped(tenantId, companyId))
            .Where(x =>
                ElectronicDocumentRetryPolicy.AutomaticRetryStates.Contains(x.CurrentState)
                && x.RetryCount > 0
            )
            .CountAsync(ct);

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

    public async Task<IReadOnlyList<ElectronicDocumentRetryCandidate>> GetRetryCandidatesAsync(
        int batchSize, CancellationToken ct = default
    )
    {
        // Bypass de filtros globales vía el accessor sancionado (AsPlatformQuery), deliberado y
        // acotado: lo usa SOLO el job de reintentos (sin contexto de tenant en Hangfire, donde el
        // filtro fail-closed devolvería 0 filas); devuelve únicamente identificadores, y cada
        // candidato se procesa después con JobExecutionContext de su propio tenant/empresa, donde
        // los filtros vuelven a aplicar (mismo patrón que GetPendingElectronicStartAsync).
        // Elegibilidad (política) y preferencia de empresa se filtran aquí, antes del lote: el job
        // procesa lo que recibe sin volver a decidir.
        var autoRetryDisabled = DefaultOnCompanySwitch.Disabling(
            _db,
            OrgSettingKeys.ElectronicDocuments.AutoRetryEnabled
        );
        var maximumSettings = RetryMaximumSettings();
        var query = _db
            .ElectronicDocuments.AsPlatformQuery()
            .AsNoTracking()
            .Where(ElectronicDocumentRetryPolicy.AutomaticRetryCandidate(DateTime.UtcNow, x => maximumSettings
                .Where(s => s.TenantId == x.TenantId && s.CompanyId == x.CompanyId)
                .Select(s => s.Value).FirstOrDefault()))
            .Where(x => !autoRetryDisabled.Any(setting =>
                setting.TenantId == x.TenantId && setting.CompanyId == x.CompanyId))
            // SRI-TOGGLE-06 — todo candidato (transmisión o consulta) solo con emisión habilitada y actividad
            // posterior a la habilitación: pausa total, y los pendientes anteriores esperan una acción explícita.
            .WhereAutomaticSriAccessAllowed(_db);
        return await BackgroundJobProgress.ReadBatchAsync(_db, "electronic-retry", query,
            x => new ElectronicDocumentRetryCandidate(x.TenantId, x.CompanyId, x.Id),
            x => x.ElectronicDocumentId, x => x.CreatedAt, batchSize, ct);
    }
}
