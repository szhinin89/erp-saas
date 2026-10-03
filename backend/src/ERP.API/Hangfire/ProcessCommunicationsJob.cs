using ERP.Application.Modules.Communications.Services;
using Hangfire;

namespace ERP.API.Hangfire;

public sealed partial class ProcessCommunicationsJob : IProcessCommunicationsJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessCommunicationsJob> _logger;

    public ProcessCommunicationsJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ProcessCommunicationsJob> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — defensa SECUNDARIA: evita trabajo inútil cuando un
    // tick se solapa con el anterior en el mismo storage de Hangfire. La exclusión real está en
    // PostgreSQL (claim FOR UPDATE SKIP LOCKED + lease + fencing en CommunicationOutboxDeliveryStore):
    // dos servidores, un atributo que falle o una llamada directa al processor siguen siendo seguros.
    [DisableConcurrentExecution(timeoutInSeconds: 10)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ICommunicationOutboxProcessor>();

        try
        {
            await processor.ProcessPendingAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogProcessCommunicationsJobFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "ProcessCommunicationsJob failed")]
    private partial void LogProcessCommunicationsJobFailed(Exception ex);
}
