using ERP.Infrastructure.Communications;
using Hangfire;

namespace ERP.API.Hangfire;

public interface IReconcileElectronicDocumentCommunicationsJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — reconciliación periódica "comprobante autorizado sin comunicación".
/// Toda la lógica (ventana, lotes, alcance por documento, idempotencia) vive en
/// <see cref="IElectronicDocumentCommunicationReconciler"/>; el atributo solo evita trabajo duplicado
/// (la exclusión real es la identidad única de la outbox).
/// </summary>
public sealed partial class ReconcileElectronicDocumentCommunicationsJob
    : IReconcileElectronicDocumentCommunicationsJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReconcileElectronicDocumentCommunicationsJob> _logger;

    public ReconcileElectronicDocumentCommunicationsJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ReconcileElectronicDocumentCommunicationsJob> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 10)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        try
        {
            await scope
                .ServiceProvider.GetRequiredService<IElectronicDocumentCommunicationReconciler>()
                .ReconcileAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogReconciliationJobFailed(ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "ReconcileElectronicDocumentCommunicationsJob failed"
    )]
    private partial void LogReconciliationJobFailed(Exception ex);
}
