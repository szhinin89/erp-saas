using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Incremento atómico de <c>tenant_subscription_usages</c> (PostgreSQL UPSERT).
/// </summary>
internal static class SubscriptionUsageIncrementer
{
    public static async Task<bool> IncrementAsync(
        ErpDbContext db,
        Guid tenantId,
        Guid featureId,
        string periodKey,
        long amount,
        CancellationToken ct)
    {
        if (IsInMemoryProvider(db))
            return await IncrementInMemoryAsync(db, tenantId, featureId, periodKey, amount, ct);

        var newId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var emptyUser = Guid.Empty;

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO tenant_subscription_usages
                (id, tenant_id, feature_id, period_key, quantity,
                 created_at, created_by, updated_at, updated_by)
            VALUES
                ({newId}, {tenantId}, {featureId}, {periodKey}, {amount},
                 {now}, {emptyUser}, {now}, {emptyUser})
            ON CONFLICT (tenant_id, feature_id, period_key)
            DO UPDATE SET
                quantity   = tenant_subscription_usages.quantity + {amount},
                updated_at = {now},
                updated_by = {emptyUser}
            """,
            ct);

        return false;
    }

    private static bool IsInMemoryProvider(ErpDbContext db) =>
        db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<bool> IncrementInMemoryAsync(
        ErpDbContext db,
        Guid tenantId,
        Guid featureId,
        string periodKey,
        long amount,
        CancellationToken ct)
    {
        var row = await db.TenantSubscriptionUsages
            .FirstOrDefaultAsync(
                u => u.TenantId == tenantId && u.FeatureId == featureId && u.PeriodKey == periodKey,
                ct);

        if (row is null)
        {
            await db.TenantSubscriptionUsages.AddAsync(
                TenantSubscriptionUsage.Create(tenantId, featureId, periodKey, amount, Guid.Empty),
                ct);
        }
        else
        {
            row.AddQuantity(amount, Guid.Empty);
        }

        return true;
    }
}
