using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Infrastructure.Services;
using Hangfire;

namespace ERP.API.Hangfire;

public interface IElectronicDocumentRetryJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Hangfire recurring job que reintenta documentos electrónicos varados en Signed/Received,
/// respetando el backoff de <see cref="ElectronicDocumentRetryPolicy"/>. Un fallo en un
/// documento nunca detiene el resto del lote. Cross-tenant por diseño (mismo patrón que
/// <see cref="ProcessOutboxJob"/>) — cada documento resuelve su propio tenant/empresa vía
/// <see cref="JobExecutionContext"/>, sin HttpContext.
/// </summary>
public sealed partial class ElectronicDocumentRetryJob : IElectronicDocumentRetryJob
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

    // A1 (auditoría de robustez): el cron dispara cada minuto ("* * * * *" en Program.cs). Si una
    // corrida tarda más de 60s (varios tenants, SOAP lento al SRI), sin este guard Hangfire
    // encolaría una segunda ejecución que podría tomar el mismo lote de candidatos — el xmin ya
    // evita el doble envío real al SRI, pero produce logs de error indistinguibles de un bug real
    // en cada solape. timeoutInSeconds corto: si otra ejecución ya tiene el lock, esta invocación
    // desiste rápido en vez de bloquear un worker de Hangfire — el próximo tick (un minuto
    // después) vuelve a intentar de todas formas.
    [DisableConcurrentExecution(timeoutInSeconds: 10)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IElectronicDocumentRepository>();
        var issuer = scope.ServiceProvider.GetRequiredService<IElectronicDocumentIssuer>();
        var preferencesResolver =
            scope.ServiceProvider.GetRequiredService<IOperationalPreferencesResolver>();

        IReadOnlyList<ElectronicDocument> candidates;
        try
        {
            candidates = await repository.GetRetryCandidatesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogCandidateQueryFailed(ex);
            return;
        }

        // CONFIG-DYNAMIC-OPERATIONS-02 (electronic_documents.auto_retry_enabled): cacheada por
        // (tenant, company) dentro de la corrida — un lote típico trae varios documentos de la
        // misma empresa, así que esto resuelve la preferencia una vez por empresa, no por
        // documento. Solo apaga el reintento AUTOMÁTICO de este job; el reintento manual
        // (issuer.RetryAsync desde la UI/API) sigue disponible sin importar esta preferencia.
        var autoRetryEnabledCache = new Dictionary<(Guid TenantId, Guid CompanyId), bool>();

        var nowUtc = DateTime.UtcNow;
        foreach (var document in candidates)
        {
            if (
                !ElectronicDocumentRetryPolicy.IsEligibleForAutomaticRetry(
                    document.RetryCount,
                    document.LastAttemptUtc,
                    nowUtc
                )
            )
                continue;

            var cacheKey = (document.TenantId, document.CompanyId);
            if (!autoRetryEnabledCache.TryGetValue(cacheKey, out var autoRetryEnabled))
            {
                var preferences = await preferencesResolver.ResolveAsync(
                    document.TenantId,
                    document.CompanyId,
                    cancellationToken
                );
                autoRetryEnabled = preferences.ElectronicDocuments.AutoRetryEnabled;
                autoRetryEnabledCache[cacheKey] = autoRetryEnabled;
            }
            if (!autoRetryEnabled)
                continue;

            using var _ = JobExecutionContext.Begin(document.TenantId, document.CompanyId);
            try
            {
                var result = await issuer.RetryAsync(
                    document.TenantId,
                    document.Id,
                    Guid.Empty,
                    cancellationToken
                );
                if (!result.IsSuccess)
                    LogRetryFailed(document.Id, result.Error);
            }
            catch (Exception ex)
            {
                LogRetryThrew(document.Id, ex);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "ElectronicDocumentRetryJob: no se pudo obtener la lista de candidatos a reintento"
    )]
    private partial void LogCandidateQueryFailed(Exception ex);

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
