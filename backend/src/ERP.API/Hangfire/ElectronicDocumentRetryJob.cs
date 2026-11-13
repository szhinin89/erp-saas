using ERP.Infrastructure.BackgroundProcessing;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Infrastructure.Services;

namespace ERP.API.Hangfire;

/// <summary>
/// Hangfire recurring job que reintenta los documentos electrónicos que la consulta de candidatos
/// declara elegibles según <see cref="ElectronicDocumentRetryPolicy"/>. Un fallo en un
/// documento nunca detiene el resto del lote. Cross-tenant por diseño (mismo patrón que
/// <see cref="RetentionElectronicRecoveryJob"/>): la consulta de candidatos ignora filtros globales
/// y devuelve solo identificadores; cada documento se procesa en su propio scope bajo
/// <see cref="JobExecutionContext"/> de su tenant/empresa, sin HttpContext.
/// </summary>
public sealed partial class ElectronicDocumentRetryJob : IRecurringJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ElectronicDocumentRetryJob> _logger;

    public ElectronicDocumentRetryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ElectronicDocumentRetryJob> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        // ZH-ELECTRONIC-RETRY-TENANT-CONTEXT-01 (ADR-036 §23.3): Hangfire no tiene HttpContext ni
        // contexto de tenant, así que la consulta de candidatos es cross-tenant y devuelve solo
        // identificadores. Antes se leían las entidades con el filtro fail-closed activo y sin
        // contexto: 0 filas, el reintento automático no procesaba nada.
        IReadOnlyList<ElectronicDocumentRetryCandidate> candidates;
        {
            await using var queryScope = _scopeFactory.CreateAsyncScope();
            candidates = await queryScope
                .ServiceProvider.GetRequiredService<IElectronicDocumentRepository>()
                .GetRetryCandidatesAsync(batchSize, cancellationToken);
            await ReportPausedAsync(queryScope.ServiceProvider, cancellationToken);
        }

        // La consulta ya aplicó la política (ElectronicDocumentRetryPolicy) y la preferencia
        // electronic_documents.auto_retry_enabled antes del lote: todo candidato es elegible. El
        // reintento manual (issuer.RetryAsync desde la UI/API) no depende de esa preferencia.
        foreach (var candidate in candidates)
        {
            // Cada documento en su propio tenant/empresa y en un scope nuevo: el issuer vuelve a
            // leer el documento con los filtros fail-closed de ESA empresa, así que un documento
            // nunca se procesa bajo el contexto de otra, y un fallo (o un DbContext en mal estado)
            // de un documento no contamina al siguiente.
            using var _ = JobExecutionContext.Begin(candidate.TenantId, candidate.CompanyId);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var result = await scope
                    .ServiceProvider.GetRequiredService<IElectronicDocumentIssuer>()
                    .RetryAsync(
                        candidate.TenantId,
                        candidate.ElectronicDocumentId,
                        Guid.Empty,
                        cancellationToken
                    );
                if (!result.IsSuccess)
                {
                    JobMetrics.RecordFailed();
                    LogRetryFailed(candidate.ElectronicDocumentId, result.Error);
                }
                else JobMetrics.RecordProcessed();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                JobMetrics.RecordFailed();
                LogRetryThrew(candidate.ElectronicDocumentId, ex);
            }
        }
    }

    /// <summary>
    /// ADR-048 — observabilidad de la pausa por estado de empresa. Nunca hace fallar el job.
    /// </summary>
    private async Task ReportPausedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            var db = services.GetService<ERP.Infrastructure.Persistence.ErpDbContext>();
            if (db is null)
                return;
            var paused = await CompanyPausedPendingQueries.ElectronicDocumentsAsync(db, 50, cancellationToken);
            CompanyPausedPendingQueries.RecordMetric("company-paused-electronic", paused);
            foreach (var row in paused)
                LogPausedByCompanyState(row.CompanyId, row.Count, row.OldestAgeHours, row.Reason.ToString());
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogPausedReportFailed(ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "ElectronicDocumentRetryJob: transmisiones nuevas pausadas por estado de empresa {CompanyId}: pendientes={PendingCount}, más antiguo={OldestAgeHours:0.0} h, motivo={PauseReason}"
    )]
    private partial void LogPausedByCompanyState(Guid companyId, long pendingCount, double oldestAgeHours, string pauseReason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "ElectronicDocumentRetryJob: no se pudo informar la pausa por estado de empresa"
    )]
    private partial void LogPausedReportFailed(Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "ElectronicDocumentRetryJob: reintento de {ElectronicDocumentId} no resolvió: {Reason}"
    )]
    private partial void LogRetryFailed(Guid electronicDocumentId, string? reason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "ElectronicDocumentRetryJob: excepción no controlada reintentando {ElectronicDocumentId}"
    )]
    private partial void LogRetryThrew(Guid electronicDocumentId, Exception ex);
}
