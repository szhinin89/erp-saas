using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

/// <summary>
/// Health check for the SaaS subscription and billing subsystem.
/// Validates: DB tables, Redis access snapshot cache, renewal job eligibility.
/// Exposed at GET /health/saas (tags: ["saas"]).
/// </summary>
public sealed class SaasSubsystemHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SaasSubsystemHealthCheck(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            // 1 — DB: billing tables accessible
            var billingAccountCount = await db.SubscriberBillingAccounts
                .AsNoTracking()
                .CountAsync(cancellationToken);
            data["billing_accounts_total"] = billingAccountCount;

            var openInvoiceCount = await db.SaasBillingInvoices
                .AsNoTracking()
                .CountAsync(x => x.Status == ERP.Domain.Billing.Enums.SaasBillingInvoiceStatus.Open, cancellationToken);
            data["open_invoices"] = openInvoiceCount;

            var processedWebhooks = await db.ProcessedWebhookEvents
                .AsNoTracking()
                .CountAsync(cancellationToken);
            data["processed_webhooks_total"] = processedWebhooks;

            // 2 — Subscriber lifecycle summary
            var subscriberSummary = await db.Subscribers
                .AsNoTracking()
                .GroupBy(s => s.LifecycleStatus)
                .Select(g => new { status = g.Key.ToString(), count = g.Count() })
                .ToListAsync(cancellationToken);

            foreach (var row in subscriberSummary)
                data[$"subscribers_{row.status.ToLowerInvariant()}"] = row.count;

            // 3 — Pending renewals (due in next 24h)
            var now     = DateTime.UtcNow;
            var horizon = now.AddHours(24);
            var pendingRenewals = await db.SubscriberBillingAccounts
                .AsNoTracking()
                .CountAsync(a =>
                    a.CurrentPeriodEndUtc.HasValue &&
                    a.CurrentPeriodEndUtc.Value >= now &&
                    a.CurrentPeriodEndUtc.Value <= horizon &&
                    a.RenewalState == ERP.Domain.Billing.Enums.BillingRenewalState.Active,
                    cancellationToken);
            data["renewals_due_24h"] = pendingRenewals;

            // 4 — Access cache (IDistributedCache connectivity)
            try
            {
                var cache = scope.ServiceProvider.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                if (cache is not null)
                {
                    var probeKey = $"saas:health:probe:{Guid.NewGuid():N}";
                    await cache.SetStringAsync(probeKey, "ok",
                        new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
                            { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5) },
                        cancellationToken);
                    await cache.RemoveAsync(probeKey, cancellationToken);
                    data["cache_status"] = "healthy";
                }
                else
                {
                    data["cache_status"] = "not_configured";
                }
            }
            catch (Exception cacheEx)
            {
                data["cache_status"]  = "degraded";
                data["cache_error"]   = cacheEx.Message;
            }

            data["checked_at_utc"] = now.ToString("O");

            return HealthCheckResult.Healthy(
                "SaaS subsystem is operational.",
                data: data);
        }
        catch (Exception ex)
        {
            data["error"] = ex.Message;
            return HealthCheckResult.Unhealthy(
                "SaaS subsystem check failed.",
                exception: ex,
                data: data);
        }
    }
}
