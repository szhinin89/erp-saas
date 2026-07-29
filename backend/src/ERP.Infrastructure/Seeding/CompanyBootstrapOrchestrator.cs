using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Único orquestador del bootstrap de una empresa nueva. No contiene lógica de negocio ni conoce
/// cómo crear sucursales, listas de precios, etc. — solo descubre los <see cref="ICompanyBootstrapStep"/>
/// registrados en DI, los ordena de forma explícita por <see cref="ICompanyBootstrapStep.Order"/>
/// (nunca por orden de registro del contenedor) y los ejecuta secuencialmente.
///
/// Implementación de <see cref="ICompanyBootstrapService"/> — el contrato público no cambia;
/// solo cambió cómo se organiza la inicialización internamente. Invocado exclusivamente desde
/// <c>ERP.Infrastructure.Services.CompanyProvisioningService</c>.
/// </summary>
public sealed partial class CompanyBootstrapOrchestrator : ICompanyBootstrapService
{
    private readonly IReadOnlyList<ICompanyBootstrapStep> _steps;
    private readonly ILogger<CompanyBootstrapOrchestrator> _logger;

    public CompanyBootstrapOrchestrator(
        IEnumerable<ICompanyBootstrapStep> steps,
        ILogger<CompanyBootstrapOrchestrator> logger)
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
        _logger = logger;
    }

    public async Task BootstrapCompanyAsync(Guid tenantId, Guid companyId, Guid actorId, CancellationToken cancellationToken = default)
    {
        LogBootstrappingCompany(companyId, tenantId);

        var context = new CompanyBootstrapContext(tenantId, companyId, actorId);

        foreach (var step in _steps)
        {
            LogRunningStep(step.GetType().Name, step.Order, companyId);
            await step.ExecuteAsync(context, cancellationToken);
        }

        LogBootstrapComplete(companyId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrapping company {CompanyId} (tenant {TenantId})…")]
    private partial void LogBootstrappingCompany(Guid companyId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running bootstrap step {StepName} (order={Order}) for company {CompanyId}.")]
    private partial void LogRunningStep(string stepName, int order, Guid companyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Company {CompanyId} bootstrap complete.")]
    private partial void LogBootstrapComplete(Guid companyId);
}
