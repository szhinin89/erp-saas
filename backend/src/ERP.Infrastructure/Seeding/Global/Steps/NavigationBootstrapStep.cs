using ERP.Infrastructure.Persistence.Navigation;

namespace ERP.Infrastructure.Seeding.Global.Steps;

/// <summary>
/// Sincroniza <c>ui_nav_groups</c>/<c>ui_nav_items</c> desde <c>KernelRegistry</c>. Delega
/// íntegramente en <see cref="NavigationSyncService"/> (sin cambios) — este step solo lo integra
/// al flujo oficial de bootstrap global. Un fallo aquí propaga y aborta el arranque de la API,
/// igual que antes de este refactor (la navegación es prerrequisito de negocio, no opcional).
/// </summary>
public sealed class NavigationBootstrapStep : IGlobalBootstrapStep
{
    public int Order => GlobalBootstrapStepOrder.Navigation;

    private readonly NavigationSyncService _navigationSync;

    public NavigationBootstrapStep(NavigationSyncService navigationSync)
    {
        _navigationSync = navigationSync;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
        => _navigationSync.SyncAsync(cancellationToken);
}
