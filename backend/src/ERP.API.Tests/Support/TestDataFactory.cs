using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Support;

/// <summary>Seeds mínimas e idempotentes para tests de integración.</summary>
internal static class TestDataFactory
{
    internal static async Task<IntegrationSeedData.SeedResult> SeedSubscriberDemoAsync(
        ErpDbContext db,
        IntegrationTestWebAppFactory factory,
        CancellationToken ct = default)
        => await IntegrationSeedData.SeedAsync(
            db,
            factory.MutableSubscriber,
            factory.MutableUser,
            ct,
            factory.MutableCompany);

    internal static async Task SeedCommercialPlansAndLimitsAsync(ErpDbContext db, CancellationToken ct = default)
    {
        await CommercialPlansBootstrap.EnsureDefaultsAsync(db);
        await CommercialPlanLimitsBootstrap.EnsureDefaultsAsync(db);
    }
}
