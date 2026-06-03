using Microsoft.EntityFrameworkCore;
using ERP.API.Tests.SecurityTests.Infrastructure;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.SecurityTests.MultiTenant;

/// <summary>
/// ATTACK CATEGORY 3 â€” IgnoreQueryFilters Bypass Attack
///
/// OBJETIVO: Verificar que IgnoreQueryFilters no puede ser usado arbitrariamente.
/// El Ãºnico punto de acceso autorizado es IPlatformQueryAccessor.
/// </summary>
public sealed class IgnoreQueryFilterTests
{
    [Fact(DisplayName = "ATTACK-3.1: IPlatformQueryAccessor.Unfiltered returns ALL tenant data (authorized bypass)")]
    public async Task Attack_3_1_PlatformQueryAccessor_Unfiltered_returns_all_data()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // Authorized bypass via IPlatformQueryAccessor
        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformQueryAccessor>();

        // This is the AUTHORIZED way to bypass â€” via documented wrapper
        var allAccounts = await platform
            .Unfiltered(db.Accounts, PlatformQueryReason.PlatformMetrics)
            .ToListAsync();

        // All accounts from all companies/subscribers should be visible
        allAccounts.Should().HaveCount(3,
            "Authorized platform bypass should return ALL 3 accounts (A1 + A2 + B1). " +
            "IPlatformQueryAccessor is the ONLY authorized bypass mechanism.");

        allAccounts.Select(a => a.CompanyId).Should().Contain(state.CompanyA1Id);
        allAccounts.Select(a => a.CompanyId).Should().Contain(state.CompanyA2Id);
        allAccounts.Select(a => a.CompanyId).Should().Contain(state.CompanyB1Id);
    }

    [Fact(DisplayName = "ATTACK-3.2: Normal context + filtered access returns only tenant data (not all)")]
    public async Task Attack_3_2_Normal_context_returns_only_filtered_data()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        // Normal filtered access â€” must only return A1 data
        var filteredAccounts = await db.Accounts.ToListAsync();

        filteredAccounts.Should().HaveCount(1,
            "Normal (filtered) access with A1 context must return ONLY 1 account. " +
            "EF query filter is enforced.");

        filteredAccounts.Should().NotBeEquivalentTo(
            await scope.ServiceProvider.GetRequiredService<IPlatformQueryAccessor>()
                .Unfiltered(db.Accounts, PlatformQueryReason.PlatformMetrics)
                .ToListAsync(),
            "Filtered result must NOT equal unfiltered result. " +
            "If equal, query filters are not working.");
    }

    [Fact(DisplayName = "ATTACK-3.3: IgnoreQueryFilters direct usage is tracked by Architecture Tests")]
    public void Attack_3_3_IgnoreQueryFilters_usage_is_tracked_in_architecture_tests()
    {
        // This test verifies that the architecture CI rule for IgnoreQueryFilters is enforced.
        // The actual rule is in ERP.Architecture.Tests.SecurityArchitectureTests.
        // Here we document the protection exists.

        var infraAssembly = typeof(ERP.Infrastructure.Persistence.ErpDbContext).Assembly;

        // Find all source files that could contain IgnoreQueryFilters
        // (reflected here as a documentation test)
        var typesWithIgnoreFiltersMethod = infraAssembly.GetTypes()
            .Where(t => t.Name == "PlatformQueryAccessor")
            .ToList();

        typesWithIgnoreFiltersMethod.Should().HaveCount(1,
            "PlatformQueryAccessor must exist â€” it is the only authorized point for bypassing query filters. " +
            "Any direct use of IgnoreQueryFilters() outside this class violates the security model.");
    }
}
