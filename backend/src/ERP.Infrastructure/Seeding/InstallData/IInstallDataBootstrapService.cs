namespace ERP.Infrastructure.Seeding.InstallData;

public interface IInstallDataBootstrapService
{
    Task ApplyPendingAsync(CancellationToken cancellationToken = default);
}
