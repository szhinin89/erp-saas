using ERP.Infrastructure.BackgroundProcessing;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Retentions.Interfaces;
using ERP.Infrastructure.Services;

namespace ERP.API.Hangfire;

/// <summary>
/// Starts only Issued retentions without an ElectronicDocument, and recovers annulment
/// finalization and verification. Existing electronic documents, including Draft, belong
/// exclusively to ElectronicDocuments. Each stream has a durable fair checkpoint.
/// </summary>
public sealed partial class RetentionElectronicRecoveryJob : IRecurringJob
{
    /// <summary>Antigüedad mínima de la emisión antes de considerarla "no iniciada".</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(2);


    /// <summary>
    /// 01B — intervalo mínimo entre consultas a ConsultaComprobante de una misma solicitud presentada. La
    /// aceptación del receptor puede tardar días: no tiene sentido consultar cada minuto (el job sí corre
    /// cada minuto); el usuario puede verificar a demanda desde la UI.
    /// </summary>
    public static readonly TimeSpan SriVerificationInterval = TimeSpan.FromMinutes(30);

    /// <summary>Pocas por corrida: cada consulta SOAP puede tardar (reintentos HTTP) y no debe frenar el resto del job.</summary>

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionElectronicRecoveryJob> _logger;

    public RetentionElectronicRecoveryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<RetentionElectronicRecoveryJob> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
    public async Task ExecuteAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 3);
        var quota = batchSize / 3;
        await FinalizeAcceptedAnnulmentsAsync(quota, cancellationToken);

        IReadOnlyList<RetentionElectronicStartCandidate> candidates;
        {
            await using var queryScope = _scopeFactory.CreateAsyncScope();
            candidates = await queryScope
                .ServiceProvider.GetRequiredService<IRetentionDocumentRepository>()
                .GetPendingElectronicStartAsync(
                    DateTime.UtcNow - GracePeriod,
                    batchSize - 2 * quota,
                    cancellationToken
                );
        }

        foreach (var candidate in candidates)
        {
            using var _ = JobExecutionContext.Begin(candidate.TenantId, candidate.CompanyId);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var transmission =
                    scope.ServiceProvider.GetRequiredService<IRetentionElectronicTransmission>();
                var result = await transmission.StartAsync(
                    candidate.TenantId,
                    candidate.CompanyId,
                    candidate.RetentionId,
                    Guid.Empty,
                    cancellationToken
                );
                if (result.IsSuccess) JobMetrics.RecordProcessed();
                else JobMetrics.RecordFailed();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                JobMetrics.RecordFailed();
                LogCandidateThrew(candidate.RetentionId, ex);
            }
        }

        await VerifySubmittedAnnulmentsAsync(quota, cancellationToken);
    }

    /// <summary>
    /// ZH-RETENTION-SRI-ANNULMENT-01B — polling de las solicitudes presentadas al SRI: cada una pasa por el
    /// MISMO <see cref="IRetentionAnnulmentService.VerifyWithSriAsync"/> que la verificación en línea
    /// (SOAP sin transacción; lock + relectura al aplicar; ANULADO finaliza una vez). Idempotente y seguro
    /// ante una verificación concurrente desde la UI. Cross-tenant: solo identificadores; cada solicitud en
    /// su scope bajo <see cref="JobExecutionContext"/>.
    /// </summary>
    private async Task VerifySubmittedAnnulmentsAsync(int batchSize, CancellationToken cancellationToken)
    {
        IReadOnlyList<(Guid TenantId, Guid CompanyId, Guid RequestId)> due;
        {
            await using var queryScope = _scopeFactory.CreateAsyncScope();
            due = await queryScope
                .ServiceProvider.GetRequiredService<IRetentionAnnulmentRequestRepository>()
                .GetDueForSriVerificationAsync(
                    DateTime.UtcNow - SriVerificationInterval,
                    batchSize,
                    cancellationToken
                );
        }

        foreach (var (tenantId, companyId, requestId) in due)
        {
            using var _ = JobExecutionContext.Begin(tenantId, companyId);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var result = await scope
                    .ServiceProvider.GetRequiredService<IRetentionAnnulmentService>()
                    .VerifyWithSriAsync(
                        tenantId,
                        companyId,
                        requestId,
                        Guid.Empty,
                        cancellationToken
                    );
                if (result.IsSuccess) JobMetrics.RecordProcessed();
                else JobMetrics.RecordFailed();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                JobMetrics.RecordFailed();
                LogCandidateThrew(requestId, ex);
            }
        }
    }

    /// <summary>
    /// ZH-RETENTION-SRI-ANNULMENT-01 — anulaciones con ANULADO confirmado cuyo documento origen no
    /// terminó de anularse (caída o fallo tras registrar la resolución): se reintenta la finalización
    /// idempotente por el mismo servicio que la resolución en línea.
    /// </summary>
    private async Task FinalizeAcceptedAnnulmentsAsync(int batchSize, CancellationToken cancellationToken)
    {
        IReadOnlyList<(Guid TenantId, Guid CompanyId, Guid RequestId)> pending;
        {
            await using var queryScope = _scopeFactory.CreateAsyncScope();
            pending = await queryScope
                .ServiceProvider.GetRequiredService<IRetentionAnnulmentRequestRepository>()
                .GetPendingFinalizationAsync(batchSize, cancellationToken);
        }

        foreach (var (tenantId, companyId, requestId) in pending)
        {
            using var _ = JobExecutionContext.Begin(tenantId, companyId);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var result = await scope
                    .ServiceProvider.GetRequiredService<IRetentionAnnulmentService>()
                    .FinalizeAsync(tenantId, companyId, requestId, Guid.Empty, cancellationToken);
                if (result.IsSuccess) JobMetrics.RecordProcessed();
                else JobMetrics.RecordFailed();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                JobMetrics.RecordFailed();
                LogCandidateThrew(requestId, ex);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "RetentionElectronicRecoveryJob: excepción no controlada recuperando la retención {RetentionId}"
    )]
    private partial void LogCandidateThrew(Guid retentionId, Exception ex);
}
