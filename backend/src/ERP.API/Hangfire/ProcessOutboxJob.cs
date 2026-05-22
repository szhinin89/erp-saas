using ERP.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Logging;

namespace ERP.API.Hangfire;

/// <summary>
/// Hangfire recurring job that drains the outbox table.
/// Runs every minute; each execution processes up to 50 pending messages.
/// Idempotent: processing an already-processed message is a no-op.
/// </summary>
public sealed class ProcessOutboxJob : IProcessOutboxJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessOutboxJob> _logger;

    public ProcessOutboxJob(IServiceScopeFactory scopeFactory, ILogger<ProcessOutboxJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

        try
        {
            await processor.ProcessPendingAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessOutboxJob failed");
        }
    }
}
