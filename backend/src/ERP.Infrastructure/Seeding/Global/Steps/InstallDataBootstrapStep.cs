using ERP.Infrastructure.Seeding.InstallData;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Global.Steps;

/// <summary>
/// Ejecuta la carga obligatoria INEC mediante <see cref="IInstallDataBootstrapService"/>.
/// Un fallo se registra y propaga: la API no puede completar el arranque con una
/// instalación incompleta.
/// </summary>
public sealed partial class InstallDataBootstrapStep : IGlobalBootstrapStep
{
    public int Order => GlobalBootstrapStepOrder.InstallData;

    private readonly IInstallDataBootstrapService _installData;
    private readonly ILogger<InstallDataBootstrapStep> _logger;

    public InstallDataBootstrapStep(
        IInstallDataBootstrapService installData,
        ILogger<InstallDataBootstrapStep> logger
    )
    {
        _installData = installData;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _installData.ApplyPendingAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogInstallDataFailed(ex);
            throw;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Mandatory InstallData failed. API startup aborted."
    )]
    private partial void LogInstallDataFailed(Exception ex);
}
