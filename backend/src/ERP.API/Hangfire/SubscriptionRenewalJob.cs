using ERP.Application.Billing.Renewal;

namespace ERP.API.Hangfire;

public interface ISubscriptionRenewalJob
{
    Task ExecuteAsync(CancellationToken ct = default);
}

/// <summary>
/// Hangfire job that triggers the renewal engine every hour.
/// Schedule: `Cron.Hourly()` in Program.cs.
///
/// Idempotent: running twice for the same hour has no effect.
/// </summary>
public sealed class SubscriptionRenewalJob : ISubscriptionRenewalJob
{
    private readonly ISubscriptionRenewalService          _renewal;
    private readonly ILogger<SubscriptionRenewalJob>      _logger;
    private readonly IConfiguration                       _config;

    public SubscriptionRenewalJob(
        ISubscriptionRenewalService renewal,
        ILogger<SubscriptionRenewalJob> logger,
        IConfiguration config)
    {
        _renewal = renewal;
        _logger  = logger;
        _config  = config;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var lookAhead = _config.GetValue("SaaS:RenewalLookAheadHours", 24);
        _logger.LogInformation("SubscriptionRenewalJob: starting with lookAhead={Hours}h", lookAhead);

        try
        {
            await _renewal.ProcessUpcomingRenewalsAsync(lookAhead, ct);
            _logger.LogInformation("SubscriptionRenewalJob: completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubscriptionRenewalJob: unhandled error");
            throw; // Hangfire marks job as failed → retries
        }
    }
}
