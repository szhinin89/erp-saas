using Microsoft.EntityFrameworkCore;
using ERP.API.Tests.SecurityTests.Infrastructure;
using ERP.API.Tests.Support;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.SecurityTests.MultiTenant;

/// <summary>
/// ATTACK CATEGORY 5 â€” Cross-Subscriber Attacks
///
/// OBJETIVO: Romper separaciÃ³n entre Subscriber A y Subscriber B.
/// </summary>
public sealed class CrossSubscriberLeakTests
{
    [Fact(DisplayName = "ATTACK-5.1: SubscriberA cannot see SubscriberB companies")]
    public async Task Attack_5_1_SubscriberA_cannot_see_SubscriberB_companies()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var companies = await db.Companies.ToListAsync();

        companies.Should().OnlyContain(c => c.SubscriberId == state.SubscriberAId,
            "CRITICAL: SubscriberA context must NEVER return SubscriberB companies.");
        companies.Should().NotContain(c => c.Id == state.CompanyB1Id,
            "CRITICAL: CompanyB1 visible in SubscriberA context â€” cross-subscriber breach.");
    }

    [Fact(DisplayName = "ATTACK-5.2: SubscriberB cannot see SubscriberA business partners")]
    public async Task Attack_5_2_SubscriberB_cannot_see_SubscriberA_business_partners()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        factory.MutableSubscriber.SubscriberId = state.SubscriberBId;
        factory.MutableCompany.CompanyId = state.CompanyB1Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var bps = await db.BusinessPartners.ToListAsync();

        bps.Should().OnlyContain(bp => bp.SubscriberId == state.SubscriberBId,
            "CRITICAL: SubscriberB context must NEVER return SubscriberA BusinessPartners.");
        bps.Should().NotContain(bp => bp.Id == state.BpA1Id || bp.Id == state.BpA2Id,
            "CRITICAL: SubscriberA BusinessPartners visible in SubscriberB context.");
    }

    [Fact(DisplayName = "ATTACK-5.3: Cross-subscriber company ID spoofing â€” A1 context with B1 company")]
    public async Task Attack_5_3_Cross_subscriber_company_id_spoofing()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // Attack: SubscriberA token + CompanyB1 ID (from different subscriber)
        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyB1Id;  // â† SPOOFED cross-subscriber

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var accounts = await db.Accounts.CountAsync();

        // The CompanyTenantInterceptor + query filter should prevent any data
        // Even if someone injects a foreign company_id, the subscriber filter
        // prevents seeing data from SubscriberB
        accounts.Should().Be(0,
            "ATTACK BLOCKED: CompanyB1 belongs to SubscriberB. " +
            "SubscriberA context with CompanyB1 ID should see 0 accounts " +
            "(subscriber filter prevents cross-subscriber company spoofing).");
    }

    [Fact(DisplayName = "ATTACK-5.4: Total data count per subscriber is exact")]
    public async Task Attack_5_4_Total_data_count_per_subscriber_is_exact()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        int bpCountA, bpCountB;

        // SubscriberA: should see 2 BPs (BpA1 + BpA2)
        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            bpCountA = await db.BusinessPartners.CountAsync();
        }

        // SubscriberB: should see 1 BP (BpB1)
        factory.MutableSubscriber.SubscriberId = state.SubscriberBId;
        factory.MutableCompany.CompanyId = state.CompanyB1Id;
        using (var scope2 = factory.Services.CreateScope())
        {
            var db = scope2.ServiceProvider.GetRequiredService<ErpDbContext>();
            bpCountB = await db.BusinessPartners.CountAsync();
        }

        bpCountA.Should().Be(2, "SubscriberA should see exactly 2 BusinessPartners (BpA1 + BpA2).");
        bpCountB.Should().Be(1, "SubscriberB should see exactly 1 BusinessPartner (BpB1).");
    }
}
