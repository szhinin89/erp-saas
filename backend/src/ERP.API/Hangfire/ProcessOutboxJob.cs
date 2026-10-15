using ERP.Infrastructure.Persistence.Outbox;
namespace ERP.API.Hangfire;
public sealed class ProcessOutboxJob(IOutboxProcessor processor) : IRecurringJob
{
    public Task ExecuteAsync(int batchSize, CancellationToken cancellationToken = default) =>
        processor.ProcessPendingAsync(batchSize, cancellationToken);
}
