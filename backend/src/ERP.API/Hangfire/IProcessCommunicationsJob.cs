namespace ERP.API.Hangfire;

/// <summary>
/// Hangfire job that processes pending tenant communications without coupling business modules to SMTP.
/// </summary>
public interface IProcessCommunicationsJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
