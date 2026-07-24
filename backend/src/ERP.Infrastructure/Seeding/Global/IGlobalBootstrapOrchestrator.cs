namespace ERP.Infrastructure.Seeding.Global;

/// <summary>
/// Único punto de entrada del bootstrap global de instalación. Invocado exclusivamente desde
/// <c>Program.cs</c> (composition root) después de aplicar migraciones — nunca desde un
/// Controller, Handler, Repository ni otro Startup task.
/// </summary>
public interface IGlobalBootstrapOrchestrator
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
