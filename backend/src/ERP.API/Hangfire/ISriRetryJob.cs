namespace ERP.API.Hangfire;

public interface ISriRetryJob
{
    Task ExecuteAsync(CancellationToken ct = default);
}
