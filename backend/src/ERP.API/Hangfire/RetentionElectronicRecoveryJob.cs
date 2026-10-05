using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Retentions.Interfaces;
using ERP.Infrastructure.Services;
using Hangfire;

namespace ERP.API.Hangfire;

public interface IRetentionElectronicRecoveryJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (ADR-036 §14) — recuperación de la transmisión inmediata.
/// La transmisión se inicia después del commit de la confirmación de Compra/Gasto; si el proceso cae
/// entre ese commit y el inicio (o la petición se corta antes de crear/avanzar el comprobante), la
/// fuente durable de lo pendiente es la propia retención: <c>Issued</c> sin ElectronicDocument (o con
/// uno que quedó en Draft). No hay un segundo pipeline: cada candidato entra por el MISMO
/// <see cref="IRetentionElectronicTransmission.StartAsync"/> que el flujo inmediato.
///
/// Idempotente: el registro es único por origen (chequeo + índice único
/// <c>uq_electronic_document_source</c>), así que dos corridas — o una corrida y la transmisión
/// inmediata — nunca crean dos ElectronicDocument. La ventana de gracia evita competir con la
/// transmisión inmediata recién iniciada. Cross-tenant por diseño (mismo patrón que
/// <see cref="ElectronicDocumentRetryJob"/>): la consulta de candidatos ignora filtros globales y
/// devuelve solo identificadores; cada candidato se procesa en su propio scope bajo
/// <see cref="JobExecutionContext"/> de su tenant/empresa.
///
/// También retoma la anulación SRI de retenciones autorizadas (ZH-RETENTION-SRI-ANNULMENT-01/01B):
/// finalizaciones pendientes tras un ANULADO y el polling de ConsultaComprobante de las solicitudes
/// presentadas.
/// </summary>
public sealed partial class RetentionElectronicRecoveryJob : IRetentionElectronicRecoveryJob
{
    /// <summary>Antigüedad mínima de la emisión antes de considerarla "no iniciada".</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(2);

    private const int BatchSize = 50;

    /// <summary>
    /// 01B — intervalo mínimo entre consultas a ConsultaComprobante de una misma solicitud presentada. La
    /// aceptación del receptor puede tardar días: no tiene sentido consultar cada minuto (el job sí corre
    /// cada minuto); el usuario puede verificar a demanda desde la UI.
    /// </summary>
    public static readonly TimeSpan SriVerificationInterval = TimeSpan.FromMinutes(30);

    /// <summary>Pocas por corrida: cada consulta SOAP puede tardar (reintentos HTTP) y no debe frenar el resto del job.</summary>
    private const int SriVerificationBatchSize = 10;

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

    [DisableConcurrentExecution(timeoutInSeconds: 10)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await FinalizeAcceptedAnnulmentsAsync(cancellationToken);

        IReadOnlyList<RetentionElectronicStartCandidate> candidates;
        try
        {
            await using var queryScope = _scopeFactory.CreateAsyncScope();
            candidates = await queryScope
                .ServiceProvider.GetRequiredService<IRetentionDocumentRepository>()
                .GetPendingElectronicStartAsync(
                    DateTime.UtcNow - GracePeriod,
                    BatchSize,
                    cancellationToken
                );
        }
        catch (Exception ex)
        {
            LogCandidateQueryFailed(ex);
            return;
        }

        foreach (var candidate in candidates)
        {
            using var _ = JobExecutionContext.Begin(candidate.TenantId, candidate.CompanyId);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var transmission =
                    scope.ServiceProvider.GetRequiredService<IRetentionElectronicTransmission>();
                await transmission.StartAsync(
                    candidate.TenantId,
                    candidate.CompanyId,
                    candidate.RetentionId,
                    Guid.Empty,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                LogCandidateThrew(candidate.RetentionId, ex);
            }
        }

        await VerifySubmittedAnnulmentsAsync(cancellationToken);
    }

    /// <summary>
    /// ZH-RETENTION-SRI-ANNULMENT-01B — polling de las solicitudes presentadas al SRI: cada una pasa por el
    /// MISMO <see cref="IRetentionAnnulmentService.VerifyWithSriAsync"/> que la verificación en línea
    /// (SOAP sin transacción; lock + relectura al aplicar; ANULADO finaliza una vez). Idempotente y seguro
    /// ante una verificación concurrente desde la UI. Cross-tenant: solo identificadores; cada solicitud en
    /// su scope bajo <see cref="JobExecutionContext"/>.
    /// </summary>
    private async Task VerifySubmittedAnnulmentsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<(Guid TenantId, Guid CompanyId, Guid RequestId)> due;
        try
        {
            await using var queryScope = _scopeFactory.CreateAsyncScope();
            due = await queryScope
                .ServiceProvider.GetRequiredService<IRetentionAnnulmentRequestRepository>()
                .GetDueForSriVerificationAsync(
                    DateTime.UtcNow - SriVerificationInterval,
                    SriVerificationBatchSize,
                    cancellationToken
                );
        }
        catch (Exception ex)
        {
            LogCandidateQueryFailed(ex);
            return;
        }

        foreach (var (tenantId, companyId, requestId) in due)
        {
            using var _ = JobExecutionContext.Begin(tenantId, companyId);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope
                    .ServiceProvider.GetRequiredService<IRetentionAnnulmentService>()
                    .VerifyWithSriAsync(
                        tenantId,
                        companyId,
                        requestId,
                        Guid.Empty,
                        cancellationToken
                    );
            }
            catch (Exception ex)
            {
                LogCandidateThrew(requestId, ex);
            }
        }
    }

    /// <summary>
    /// ZH-RETENTION-SRI-ANNULMENT-01 — anulaciones con ANULADO confirmado cuyo documento origen no
    /// terminó de anularse (caída o fallo tras registrar la resolución): se reintenta la finalización
    /// idempotente por el mismo servicio que la resolución en línea.
    /// </summary>
    private async Task FinalizeAcceptedAnnulmentsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<(Guid TenantId, Guid CompanyId, Guid RequestId)> pending;
        try
        {
            await using var queryScope = _scopeFactory.CreateAsyncScope();
            pending = await queryScope
                .ServiceProvider.GetRequiredService<IRetentionAnnulmentRequestRepository>()
                .GetPendingFinalizationAsync(BatchSize, cancellationToken);
        }
        catch (Exception ex)
        {
            LogCandidateQueryFailed(ex);
            return;
        }

        foreach (var (tenantId, companyId, requestId) in pending)
        {
            using var _ = JobExecutionContext.Begin(tenantId, companyId);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope
                    .ServiceProvider.GetRequiredService<IRetentionAnnulmentService>()
                    .FinalizeAsync(tenantId, companyId, requestId, Guid.Empty, cancellationToken);
            }
            catch (Exception ex)
            {
                LogCandidateThrew(requestId, ex);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "RetentionElectronicRecoveryJob: no se pudo obtener la lista de retenciones pendientes de transmisión"
    )]
    private partial void LogCandidateQueryFailed(Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "RetentionElectronicRecoveryJob: excepción no controlada recuperando la retención {RetentionId}"
    )]
    private partial void LogCandidateThrew(Guid retentionId, Exception ex);
}
