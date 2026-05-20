using Microsoft.EntityFrameworkCore;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Tests.Persistence;

internal static class UnifiedIntegrationTestSeed
{
    private static bool _seeded;

    public static async Task EnsureAsync(ErpDbContext ctx, CancellationToken ct = default)
    {
        if (_seeded && await ctx.Subscribers.AnyAsync(t => t.Id == UnifiedDocumentSyncIntegrationTests.SubscriberId, ct))
            return;

        var now = DateTime.UtcNow;
        var tid = UnifiedDocumentSyncIntegrationTests.SubscriberId;
        var uid = UnifiedDocumentSyncIntegrationTests.UserId;
        var cid = UnifiedDocumentSyncIntegrationTests.CustomerId;
        var bid = UnifiedDocumentSyncIntegrationTests.BranchId;
        var wid = UnifiedDocumentSyncIntegrationTests.WarehouseId;
        var companyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var estabId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        await ctx.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO subscribers (id, name, slug, is_active, password_reset_mode, display_order, priority, electronic_billing_trial_enabled, "SubscriberId", created_at, created_by)
            VALUES ({0}, 'Test Tenant', 'test-tenant', true, 0, 0, 0, false, {0}, {1}, {2})
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO company (id, subscriber_id, ruc, legal_name, main_address, country_code, created_at, updated_at)
            VALUES ({3}, {0}, '1799999999001', 'Test Company', 'Quito', 'ECU', {1}, {1})
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO establishment (id, company_id, code, name, address, is_main, is_active, created_at)
            VALUES ({4}, {3}, '001', 'Matriz', 'Quito', true, true, {1})
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO branches (id, name, address, is_main_branch, subscriber_id, created_at, created_by, is_active)
            VALUES ({5}, 'Sucursal Test', 'Quito', true, {0}, {1}, {2}, true)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO warehouse (id, establishment_id, name, subscriber_id, created_at, created_by, is_active)
            VALUES ({6}, {4}, 'Bodega Test', {0}, {1}, {2}, true)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO customers (id, identification_type, identification_number, legal_name, payment_days, subscriber_id, created_at, created_by, is_active)
            VALUES ({7}, 'RUC', '1799999999001', 'Cliente Test', 0, {0}, {1}, {2}, true)
            ON CONFLICT (id) DO NOTHING;
            """,
            tid, now, uid, companyId, estabId, bid, wid, cid);

        _seeded = true;
    }
}
