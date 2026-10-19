using ERP.Application.Setup;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Setup;

public sealed class FirstRunSession(FirstRunOwnershipCoordinator coordinator, ErpDbContext db) : IFirstRunSession
{
    public Task RunStartupAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) =>
        coordinator.RunAsync(db, true, async ct => { await operation(ct); return true; }, cancellationToken);

    public Task<T> RunInstallationAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
        coordinator.RunAsync(db, false, operation, cancellationToken);
}
