using ERP.Infrastructure.Seeding.InstallData;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Global.Steps;

/// <summary>
/// Ejecuta los scripts embebidos de datos globales inmutables (geografía, país). Delega
/// íntegramente en <see cref="IInstallDataBootstrapService"/> (sin cambios). A diferencia de
/// <see cref="NavigationBootstrapStep"/>, un fallo aquí NO debe bloquear el arranque de la API —
/// se captura y registra dentro de este step (mismo comportamiento que tenía el bloque try/catch
/// en <c>Program.cs</c> antes de este refactor). El orquestador nunca decide qué step es
/// tolerante a fallos; esa decisión es responsabilidad de cada step.
/// </summary>
public sealed partial class InstallDataBootstrapStep : IGlobalBootstrapStep
{
    public int Order => GlobalBootstrapStepOrder.InstallData;

    private readonly IInstallDataBootstrapService _installData;
    private readonly ILogger<InstallDataBootstrapStep> _logger;

    public InstallDataBootstrapStep(IInstallDataBootstrapService installData, ILogger<InstallDataBootstrapStep> logger)
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
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "InstallData failed at startup. Continuing without blocking API startup.")]
    private partial void LogInstallDataFailed(Exception ex);
}
