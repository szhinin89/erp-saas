using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

/// <summary>/health/query-filter-enforcement — DbContext debe tener filtros globales activos.</summary>
public sealed class QueryFilterEnforcementHealthCheck : IHealthCheck
{
    private readonly ErpDbContext _db;

    public QueryFilterEnforcementHealthCheck(ErpDbContext db) => _db = db;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var model = _db.Model;
        var filteredEntities = model.GetEntityTypes()
            .Count(e => e.GetQueryFilter() is not null);

        if (filteredEntities == 0)
            return Task.FromResult(HealthCheckResult.Unhealthy("No global query filters configured."));

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Query filters active on {filteredEntities} entity type(s)."));
    }
}
