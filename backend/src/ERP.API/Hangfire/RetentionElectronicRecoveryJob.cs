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
/// </summary>
public sealed partial class RetentionElectronicRecoveryJob : IRetentionElectronicRecoveryJob
{
    /// <summary>Antigüedad mínima de la emisión antes de considerarla "no iniciada".</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(2);

    private const int BatchSize = 50;

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
