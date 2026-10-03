using ERP.Application.Modules.Communications.ElectronicDocuments;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Communications;

public interface IElectronicDocumentCommunicationReconciler
{
    Task<ElectronicDocumentCommunicationReconciliationSummary> ReconcileAsync(CancellationToken ct = default);
}

/// <param name="Examined">Comprobantes entregados al servicio en esta corrida (≤ <see cref="ElectronicDocumentCommunicationReconciler.MaxPerRun"/>).</param>
/// <param name="Wrapped">La corrida llegó al final de los faltantes: la próxima vuelve a empezar por el más antiguo.</param>
public sealed record ElectronicDocumentCommunicationReconciliationSummary(
    int Examined,
    int Queued,
    int AlreadyQueued,
    int Skipped,
    int Failed,
    bool Wrapped = true
);

/// <summary>
/// Posición de la reconciliación entre corridas (keyset del último comprobante examinado). Singleton
/// process-local, sin tabla: perderlo (reinicio) solo hace que la siguiente corrida empiece otra vez por
/// el más antiguo — nunca se pierde un faltante, a lo sumo se reexamina.
/// </summary>
public sealed class ElectronicDocumentCommunicationReconciliationCursor
{
    private readonly object _gate = new();
    private ElectronicDocumentCommunicationCandidate? _resumeAfter;

    public ElectronicDocumentCommunicationCandidate? ResumeAfter
    {
        get { lock (_gate) return _resumeAfter; }
        set { lock (_gate) _resumeAfter = value; }
    }
}

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — red de seguridad del evento: un comprobante Authorized cuya
/// comunicación no llegó a encolarse (proceso caído entre la autorización y el handler, fallo absorbido
/// por el handler, despliegue intermedio, autorización tardía) se encola por el MISMO
/// <see cref="IElectronicDocumentCommunicationService"/> que el evento, con la MISMA identidad: el
/// evento, un evento duplicado y la reconciliación — incluso concurrentes — producen una sola fila
/// (INSERT … ON CONFLICT DO NOTHING en PostgreSQL).
/// <para>
/// Verificación final — horizonte: <b>sin límite de antigüedad</b>. Un faltante nunca queda fuera solo por
/// viejo. Orden determinístico y global, más antiguos primero (<c>created_at</c>, <c>id</c>):
/// <list type="bullet">
/// <item>Antigüedad mínima <see cref="MinimumAge"/> desde el último cambio (no compite con el evento).</item>
/// <item>Lote acotado: como máximo <see cref="MaxPerRun"/> comprobantes por corrida, en páginas de
/// <see cref="PageSize"/>.</item>
/// <item>Progreso garantizado: la corrida continúa donde terminó la anterior
/// (<see cref="ElectronicDocumentCommunicationReconciliationCursor"/>) y vuelve al más antiguo al llegar
/// al final. Los faltantes que el servicio omite y siguen faltando (origen aún no autorizado o inexistente)
/// se reexaminan en la vuelta siguiente, pero nunca bloquean a los posteriores.</item>
/// <item>Empresas con la preferencia desactivada se excluyen en SQL: no consumen el lote.</item>
/// </list>
/// No reencola filas Failed (la outbox ya tiene la evidencia; reencolar es una acción manual futura).
/// </para>
/// <para>
/// Cross-tenant con el patrón autorizado: la consulta solo devuelve identificadores; la preferencia de
/// cada empresa y cada comprobante se procesan en su propio scope bajo <see cref="JobExecutionContext"/>.
/// </para>
/// </summary>
public sealed partial class ElectronicDocumentCommunicationReconciler : IElectronicDocumentCommunicationReconciler
{
    public static readonly TimeSpan MinimumAge = TimeSpan.FromMinutes(5);
    public const int PageSize = 50;
    public const int MaxPerRun = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ElectronicDocumentCommunicationReconciliationCursor _cursor;
    private readonly TimeProvider _time;
    private readonly ILogger<ElectronicDocumentCommunicationReconciler> _logger;
    private readonly int _maxPerRun;

    /// <param name="maxPerRun">Tamaño del lote; producción usa <see cref="MaxPerRun"/> (los tests lo reducen).</param>
    public ElectronicDocumentCommunicationReconciler(
        IServiceScopeFactory scopeFactory,
        ElectronicDocumentCommunicationReconciliationCursor cursor,
        TimeProvider time,
        ILogger<ElectronicDocumentCommunicationReconciler> logger,
        int maxPerRun = MaxPerRun
    )
    {
        _maxPerRun = Math.Clamp(maxPerRun, 1, MaxPerRun);
        _scopeFactory = scopeFactory;
        _cursor = cursor;
        _time = time;
        _logger = logger;
    }

    public async Task<ElectronicDocumentCommunicationReconciliationSummary> ReconcileAsync(CancellationToken ct = default)
    {
        var lastChangeBefore = _time.GetUtcNow().UtcDateTime - MinimumAge;

        IReadOnlyList<ElectronicDocumentCommunicationRoute> routes;
        await using (var scope = _scopeFactory.CreateAsyncScope())
            routes = scope.ServiceProvider.GetRequiredService<IElectronicDocumentCommunicationContributorResolver>().Routes;

        var summary = new Counter();
        var preferenceByCompany = new Dictionary<Guid, bool>();
        var excludedCompanies = new List<Guid>();
        var after = _cursor.ResumeAfter;
        var reachedEnd = false;

        while (summary.Examined < _maxPerRun)
        {
            IReadOnlyList<ElectronicDocumentCommunicationCandidate> page;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                page = await scope.ServiceProvider
                    .GetRequiredService<IElectronicDocumentCommunicationReconciliationQuery>()
                    .GetMissingAsync(routes, lastChangeBefore, after, excludedCompanies, PageSize, ct);
            }

            foreach (var candidate in page)
            {
                if (summary.Examined >= _maxPerRun)
                    break;
                after = candidate;

                if (!preferenceByCompany.TryGetValue(candidate.CompanyId, out var enabled))
                {
                    enabled = await IsEmailOnAuthorizationEnabledAsync(candidate.TenantId, candidate.CompanyId, ct);
                    preferenceByCompany[candidate.CompanyId] = enabled;
                    if (!enabled)
                        excludedCompanies.Add(candidate.CompanyId);
                }

                if (enabled)
                    await ReconcileOneAsync(candidate, summary, ct);
            }

            if (page.Count < PageSize && (page.Count == 0 || after == page[^1]))
            {
                reachedEnd = true;
                break;
            }
        }

        // Al final de los faltantes la próxima corrida vuelve al más antiguo; si no, continúa aquí.
        _cursor.ResumeAfter = reachedEnd ? null : after;

        var result = summary.ToSummary(reachedEnd);
        if (result.Examined > 0)
            LogRunCompleted(result.Examined, result.Queued, result.AlreadyQueued, result.Skipped, result.Failed, reachedEnd);
        return result;
    }

    private async Task ReconcileOneAsync(ElectronicDocumentCommunicationCandidate candidate, Counter summary, CancellationToken ct)
    {
        summary.Examined++;
        using var _ = JobExecutionContext.Begin(candidate.TenantId, candidate.CompanyId);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var document = await scope.ServiceProvider
                .GetRequiredService<IElectronicDocumentRepository>()
                .GetByIdAsync(candidate.TenantId, candidate.ElectronicDocumentId, ct);
            if (document is null || document.CompanyId != candidate.CompanyId)
            {
                summary.Skipped++;
                return;
            }

            var result = await scope.ServiceProvider
                .GetRequiredService<IElectronicDocumentCommunicationService>()
                .RequestAsync(document, ElectronicDocumentCommunicationTrigger.Reconciliation, ct);

            switch (result.Outcome)
            {
                case ElectronicDocumentCommunicationOutcome.Queued:
                    summary.Queued++;
                    LogReconciled(candidate.ElectronicDocumentId, candidate.TenantId, candidate.CompanyId, result.CommunicationId!.Value, result.FailureCode);
                    break;
                case ElectronicDocumentCommunicationOutcome.AlreadyQueued:
                    summary.AlreadyQueued++;
                    break;
                default:
                    summary.Skipped++;
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            summary.Failed++;
            LogCandidateFailed(candidate.ElectronicDocumentId, ex);
        }
    }

    private async Task<bool> IsEmailOnAuthorizationEnabledAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        using var _ = JobExecutionContext.Begin(tenantId, companyId);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var preferences = await scope.ServiceProvider
            .GetRequiredService<IOperationalPreferencesResolver>()
            .ResolveAsync(tenantId, companyId, ct);
        return preferences.ElectronicDocuments.EmailOnAuthorization;
    }

    private sealed class Counter
    {
        public int Examined;
        public int Queued;
        public int AlreadyQueued;
        public int Skipped;
        public int Failed;

        public ElectronicDocumentCommunicationReconciliationSummary ToSummary(bool wrapped) =>
            new(Examined, Queued, AlreadyQueued, Skipped, Failed, wrapped);
    }

    // Sin PII: solo identificadores y códigos.
    [LoggerMessage(EventId = 4235, EventName = "ElectronicDocumentCommunicationReconciled", Level = LogLevel.Information,
        Message = "Communications: reconciled ElectronicDocument {ElectronicDocumentId} tenant={TenantId} company={CompanyId} -> {CommunicationId} failure={FailureCode}")]
    private partial void LogReconciled(Guid electronicDocumentId, Guid tenantId, Guid companyId, Guid communicationId, string? failureCode);

    [LoggerMessage(EventId = 4236, EventName = "ElectronicDocumentCommunicationReconciliationFailed", Level = LogLevel.Warning,
        Message = "Communications: reconciliation failed for ElectronicDocument {ElectronicDocumentId}; it will be retried on the next run")]
    private partial void LogCandidateFailed(Guid electronicDocumentId, Exception ex);

    [LoggerMessage(EventId = 4237, EventName = "ElectronicDocumentCommunicationReconciliationRun", Level = LogLevel.Information,
        Message = "Communications: reconciliation examined={Examined} queued={Queued} alreadyQueued={AlreadyQueued} skipped={Skipped} failed={Failed} wrapped={Wrapped}")]
    private partial void LogRunCompleted(int examined, int queued, int alreadyQueued, int skipped, int failed, bool wrapped);
}
