using Microsoft.EntityFrameworkCore;
using ERP.API.Tests.SecurityTests.Infrastructure;
using ERP.API.Tests.Support;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.SecurityTests.MultiTenant;

/// <summary>
/// ATTACK CATEGORY 1 â€” Cross-Company Data Leak Attacks
///
/// OBJETIVO: Verificar que Company A JAMÃS ve Company B data.
///
/// Todos los tests deben PASAR (zero-leak garantizado).
/// Cualquier fallo indica una brecha de aislamiento multi-tenant.
/// </summary>
public sealed class CrossCompanyLeakTests
{
    // â”€â”€ ATTACK 1.1: Company A1 no puede ver datos de Company A2 (mismo subscriber) â”€â”€

    [Fact(DisplayName = "ATTACK-1.1: Company A1 cannot see Company A2 accounts (same subscriber)")]
    public async Task Attack_1_1_Company_A1_cannot_see_A2_accounts()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // Attack: Switch context to Company A1 â€” should NOT see A2 accounts
        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;

        var accounts = await db.Accounts.ToListAsync();

        // Zero-Leak Assert: only A1 account visible
        accounts.Should().OnlyContain(a => a.CompanyId == state.CompanyA1Id,
            "Company A1 context must NEVER return Company A2 accounts. " +
            "Cross-company account leak detected.");

        accounts.Should().NotContain(a => a.CompanyId == state.CompanyA2Id,
            "CRITICAL LEAK: A1 context returned A2 account (AccA2).");

        accounts.Should().NotContain(a => a.CompanyId == state.CompanyB1Id,
            "CRITICAL LEAK: A1 context returned B1 account (cross-subscriber).");
    }

    [Fact(DisplayName = "ATTACK-1.2: Company A2 cannot see Company A1 accounts (same subscriber)")]
    public async Task Attack_1_2_Company_A2_cannot_see_A1_accounts()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA2Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var accounts = await db.Accounts.ToListAsync();

        accounts.Should().OnlyContain(a => a.CompanyId == state.CompanyA2Id,
            "Company A2 context must NEVER return Company A1 accounts.");
        accounts.Should().NotContain(a => a.Id == state.AccA1Id,
            "CRITICAL LEAK: A2 context returned A1 specific account.");
    }

    // â”€â”€ ATTACK 1.3: Company A1 no puede ver datos de Company B1 (diferente subscriber) â”€â”€

    [Fact(DisplayName = "ATTACK-1.3: Company A1 cannot see Company B1 accounts (different subscriber)")]
    public async Task Attack_1_3_Company_A1_cannot_see_B1_accounts()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var accounts = await db.Accounts.ToListAsync();

        accounts.Should().NotContain(a => a.SubscriberId == state.SubscriberBId,
            "CRITICAL LEAK: SubscriberA context returned SubscriberB data. Cross-subscriber breach.");
        accounts.Should().NotContain(a => a.Id == state.AccB1Id,
            "CRITICAL LEAK: A1 context returned B1 specific account.");
    }

    // â”€â”€ ATTACK 1.4: BusinessPartner cross-company check â”€â”€

    [Fact(DisplayName = "ATTACK-1.4: BusinessPartners are subscriber-scoped â€” correct cross-company visibility")]
    public async Task Attack_1_4_BusinessPartners_subscriber_scoped_correct_visibility()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // BusinessPartner is subscriber-scoped (shared across companies of SAME subscriber)
        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var bps = await db.BusinessPartners.ToListAsync();

        // Within same subscriber: A1 and A2 BPs should be visible (subscriber-scoped)
        bps.Should().Contain(bp => bp.Id == state.BpA1Id,
            "BusinessPartner BpA1 should be visible within SubscriberA.");
        bps.Should().Contain(bp => bp.Id == state.BpA2Id,
            "BusinessPartner BpA2 should be visible within SubscriberA (subscriber-shared).");

        // Cross-subscriber: B1 BP must NOT be visible
        bps.Should().NotContain(bp => bp.Id == state.BpB1Id,
            "CRITICAL LEAK: SubscriberA context returned SubscriberB BusinessPartner.");
        bps.Should().NotContain(bp => bp.SubscriberId == state.SubscriberBId,
            "CRITICAL LEAK: Cross-subscriber BusinessPartner leak detected.");
    }

    // â”€â”€ ATTACK 1.5: Count assertion â€” exact data isolation â”€â”€

    [Fact(DisplayName = "ATTACK-1.5: Exact count â€” each company sees ONLY its own accounts")]
    public async Task Attack_1_5_Exact_count_company_sees_only_own_accounts()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // Test Company A1: should see exactly 1 account
        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;

        int countA1, countA2, countB1;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            countA1 = await db.Accounts.CountAsync();
        }

        // Test Company A2: should see exactly 1 account
        factory.MutableCompany.CompanyId = state.CompanyA2Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            countA2 = await db.Accounts.CountAsync();
        }

        // Test Company B1: should see exactly 1 account (different subscriber)
        factory.MutableSubscriber.SubscriberId = state.SubscriberBId;
        factory.MutableCompany.CompanyId = state.CompanyB1Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            countB1 = await db.Accounts.CountAsync();
        }

        countA1.Should().Be(1, "Company A1 must see exactly 1 account (its own).");
        countA2.Should().Be(1, "Company A2 must see exactly 1 account (its own).");
        countB1.Should().Be(1, "Company B1 must see exactly 1 account (its own).");

        // Cross-company sum: 3 accounts total, each company sees exactly 1
        (countA1 + countA2 + countB1).Should().Be(3,
            "Total visible across all contexts = 3 (1 per company, no overlap).");
    }
}
