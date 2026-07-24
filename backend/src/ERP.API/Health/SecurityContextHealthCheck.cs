using ERP.Application.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

/// <summary>/health/security-context — verifica servicios de contexto tenant/empresa.</summary>
public sealed class SecurityContextHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _services;

    public SecurityContextHealthCheck(IServiceProvider services) => _services = services;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var tenant = scope.ServiceProvider.GetService<ICurrentTenant>();
        var company = scope.ServiceProvider.GetService<ICurrentCompany>();
        var user = scope.ServiceProvider.GetService<ICurrentUser>();

        if (tenant is null || company is null || user is null)
            return Task.FromResult(HealthCheckResult.Unhealthy("Servicios de contexto no registrados."));

        return Task.FromResult(HealthCheckResult.Healthy("Context services registered."));
    }
}
