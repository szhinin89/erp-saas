using ERP.Application.Modules.Communications.Services;

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
