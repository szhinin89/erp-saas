namespace ERP.API.Hangfire;

/// <summary>
/// Hangfire job that processes pending outbox messages.
/// Runs on a schedule to ensure all domain events are marked as processed
/// and — in future phases — forwarded to external systems.
/// </summary>
public interface IProcessOutboxJob
{
    Task ExecuteAsync(CancellationToken ct = default);
}
