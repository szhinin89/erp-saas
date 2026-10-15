using ERP.Infrastructure.BackgroundProcessing;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Infrastructure.Services;

namespace ERP.API.Hangfire;

/// <summary>
/// Hangfire recurring job que reintenta documentos electrónicos varados en Signed/Received,
/// respetando el backoff de <see cref="ElectronicDocumentRetryPolicy"/>. Un fallo en un
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
        }

        // CONFIG-DYNAMIC-OPERATIONS-02 (electronic_documents.auto_retry_enabled): cacheada por
        // (tenant, company) dentro de la corrida — un lote típico trae varios documentos de la
        // misma empresa, así que esto resuelve la preferencia una vez por empresa, no por
        // documento. Solo apaga el reintento AUTOMÁTICO de este job; el reintento manual
        // (issuer.RetryAsync desde la UI/API) sigue disponible sin importar esta preferencia.
        var autoRetryEnabledCache = new Dictionary<(Guid TenantId, Guid CompanyId), bool>();

        var nowUtc = DateTime.UtcNow;
        foreach (var candidate in candidates)
        {
            if (
                !ElectronicDocumentRetryPolicy.IsEligibleForAutomaticRetry(
                    candidate.RetryCount,
                    candidate.LastAttemptUtc,
                    nowUtc
                )
            )
                continue;

            // Cada documento en su propio tenant/empresa y en un scope nuevo: el issuer vuelve a
            // leer el documento con los filtros fail-closed de ESA empresa, así que un documento
            // nunca se procesa bajo el contexto de otra, y un fallo (o un DbContext en mal estado)
            // de un documento no contamina al siguiente.
            using var _ = JobExecutionContext.Begin(candidate.TenantId, candidate.CompanyId);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var cacheKey = (candidate.TenantId, candidate.CompanyId);
                if (!autoRetryEnabledCache.TryGetValue(cacheKey, out var autoRetryEnabled))
                {
                    var preferences = await scope
                        .ServiceProvider.GetRequiredService<IOperationalPreferencesResolver>()
                        .ResolveAsync(candidate.TenantId, candidate.CompanyId, cancellationToken);
                    autoRetryEnabled = preferences.ElectronicDocuments.AutoRetryEnabled;
                    autoRetryEnabledCache[cacheKey] = autoRetryEnabled;
                }
                if (!autoRetryEnabled)
                    continue;

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
