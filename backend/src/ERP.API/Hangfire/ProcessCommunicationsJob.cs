using ERP.Application.Modules.Communications.Services;
namespace ERP.API.Hangfire;
public sealed class ProcessCommunicationsJob(ICommunicationOutboxProcessor processor) : IRecurringJob
{
    public Task ExecuteAsync(int batchSize, CancellationToken cancellationToken = default) =>
        processor.ProcessPendingAsync(batchSize, cancellationToken);
}
