using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Global;

/// <summary>
/// Único orquestador del bootstrap global de instalación. Igual que
/// <c>CompanyBootstrapOrchestrator</c>: no contiene lógica de negocio, no conoce cómo sincronizar
/// navegación ni cómo cargar datos de instalación — solo descubre los
/// <see cref="IGlobalBootstrapStep"/> registrados en DI, los ordena de forma explícita por
/// <see cref="IGlobalBootstrapStep.Order"/> (nunca por orden de registro) y los ejecuta
/// secuencialmente.
/// </summary>
public sealed partial class GlobalBootstrapOrchestrator : IGlobalBootstrapOrchestrator
{
    private readonly IReadOnlyList<IGlobalBootstrapStep> _steps;
    private readonly ILogger<GlobalBootstrapOrchestrator> _logger;

    public GlobalBootstrapOrchestrator(
        IEnumerable<IGlobalBootstrapStep> steps,
        ILogger<GlobalBootstrapOrchestrator> logger
    )
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        LogRunningGlobalBootstrap();

        foreach (var step in _steps)
        {
            LogRunningStep(step.GetType().Name, step.Order);
            await step.ExecuteAsync(cancellationToken);
        }

        LogGlobalBootstrapComplete();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running global bootstrap…")]
    private partial void LogRunningGlobalBootstrap();

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Running global bootstrap step {StepName} (order={Order})."
    )]
    private partial void LogRunningStep(string stepName, int order);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Global bootstrap complete.")]
    private partial void LogGlobalBootstrapComplete();
}
