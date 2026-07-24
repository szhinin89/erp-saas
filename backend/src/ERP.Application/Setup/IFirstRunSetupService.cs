namespace ERP.Application.Setup;

/// <summary>
/// Ensures a fresh, single-use setup token exists while the system is uninitialized,
/// and prints it to the console — the only place the raw token is ever exposed.
/// </summary>
public interface IFirstRunSetupService
{
    Task EnsureSetupTokenAsync(CancellationToken cancellationToken = default);
}
