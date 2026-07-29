using ERP.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

public sealed partial class ErpDbContextReadyHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErpDbContextReadyHealthCheck> _logger;

    public ErpDbContextReadyHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<ErpDbContextReadyHealthCheck> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var provider = db.Database.ProviderName ?? string.Empty;
            LogDatabaseProvider(provider);

            // Integration tests use InMemory provider; readiness should stay healthy.
            if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
                return HealthCheckResult.Healthy("InMemory provider is ready.");

            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            LogCanConnectResult(canConnect);
            return canConnect
                ? HealthCheckResult.Healthy("Database connection is ready.")
                : HealthCheckResult.Unhealthy("Database connection is not ready.");
        }
        catch (Exception ex)
        {
            LogDatabaseCheckFailed(ex);
            return HealthCheckResult.Unhealthy("Database readiness check failed.", ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Health/ready database provider: {Provider}"
    )]
    private partial void LogDatabaseProvider(string provider);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Health/ready CanConnect result: {CanConnect}"
    )]
    private partial void LogCanConnectResult(bool canConnect);

    [LoggerMessage(Level = LogLevel.Error, Message = "Health/ready database check failed.")]
    private partial void LogDatabaseCheckFailed(Exception ex);
}
