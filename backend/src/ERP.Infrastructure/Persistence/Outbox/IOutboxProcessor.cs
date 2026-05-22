namespace ERP.Infrastructure.Persistence.Outbox;

/// <summary>
/// Processes pending outbox messages.
/// Currently marks them as processed (foundation phase).
/// Future: forwards events to external message bus, analytics pipeline, or automation layer.
/// </summary>
public interface IOutboxProcessor
{
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
}
