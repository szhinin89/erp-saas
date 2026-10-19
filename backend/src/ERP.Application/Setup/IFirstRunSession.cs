namespace ERP.Application.Setup;

/// <summary>Runs the existing setup flow on the exclusive installation session.</summary>
public interface IFirstRunSession
{
    Task RunStartupAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
    Task<T> RunInstallationAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
