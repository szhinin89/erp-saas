using ERP.Application.Common;
using ERP.Domain.Access.Entities;
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
        await CommercialPlansBootstrap.EnsureDefaultsAsync(db, ct);
        await CommercialPlanFeaturesBootstrap.EnsureDefaultsAsync(db, ct);
        await CommercialPlanLimitsBootstrap.EnsureDefaultsAsync(db, ct);
    }

    internal static async Task<Guid> SeedPlatformOperatorAsync(
        ErpDbContext db,
        MutableCurrentUser factoryUser,
        CancellationToken ct = default)
    {
        var userId = Guid.NewGuid();
        var user = IdentityUser.CreatePlatformOperator(
            "Platform",
            "Operator",
            $"superadmin-{userId:N}@test.local",
            passwordHash: "$2a$11$test.hash.only.for.integration.tests",
            createdBy: userId);
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync(ct);
        factoryUser.UserId = user.Id;
        return user.Id;
    }
}
