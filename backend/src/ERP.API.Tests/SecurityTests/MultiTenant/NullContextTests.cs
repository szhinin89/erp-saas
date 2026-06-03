using Microsoft.EntityFrameworkCore;
using ERP.API.Tests.SecurityTests.Infrastructure;
using ERP.API.Tests.Support;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.SecurityTests.MultiTenant;

/// <summary>
/// ATTACK CATEGORY 4 â€” Null Context Attack (No Tenant Injection)
///
/// OBJETIVO: Verificar que sin contexto vÃ¡lido el resultado es SIEMPRE 0 filas.
/// GARANTÃA: FAIL-CLOSED â€” ningÃºn dato accesible sin tenant.
/// </summary>
public sealed class NullContextTests
{
    [Fact(DisplayName = "ATTACK-4.1: No subscriber context â†’ 0 accounts (fail-closed)")]
    public async Task Attack_4_1_No_subscriber_context_returns_zero_accounts()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // Attack: null subscriber context (SubscriberId = Guid.Empty)
        factory.MutableSubscriber.SubscriberId = Guid.Empty;
        factory.MutableCompany.CompanyId = Guid.Empty;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var count = await db.Accounts.CountAsync();

        count.Should().Be(0,
            "FAIL-CLOSED: Without subscriber context, zero accounts must be visible. " +
            "EF global query filter must reject all rows when SubscriberId = Guid.Empty.");
    }

    [Fact(DisplayName = "ATTACK-4.2: No company context â†’ 0 accounts (fail-closed)")]
    public async Task Attack_4_2_No_company_context_returns_zero_accounts()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // Attack: valid subscriber but NO company context
        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = Guid.Empty;  // no company

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var accounts = await db.Accounts.CountAsync();

        accounts.Should().Be(0,
            "FAIL-CLOSED: Accounts are company-scoped. Without CompanyId = Guid.Empty, " +
            "the ICompanyScopedEntity filter must return 0 rows. " +
            "Never return all-company data when company context is absent.");
    }

    [Fact(DisplayName = "ATTACK-4.3: No context â†’ 0 BusinessPartners (subscriber-scoped fail-closed)")]
    public async Task Attack_4_3_No_context_returns_zero_business_partners()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        await SecurityTestSeeder.SeedAsync(factory.Services);

        factory.MutableSubscriber.SubscriberId = Guid.Empty;
        factory.MutableCompany.CompanyId = Guid.Empty;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var count = await db.BusinessPartners.CountAsync();

        count.Should().Be(0,
            "FAIL-CLOSED: BusinessPartners are subscriber-scoped. " +
            "Without SubscriberId context, zero BusinessPartners visible.");
    }

    [Fact(DisplayName = "ATTACK-4.4: Wrong subscriber â†’ 0 rows (cross-subscriber fail-closed)")]
    public async Task Attack_4_4_Wrong_subscriber_returns_zero_accounts()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // Attack: fabricated subscriber ID (not registered in system)
        var fabricatedSubscriberId = Guid.NewGuid();
        factory.MutableSubscriber.SubscriberId = fabricatedSubscriberId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id; // valid company, wrong subscriber

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var accounts = await db.Accounts.CountAsync();
        var bps = await db.BusinessPartners.CountAsync();

        accounts.Should().Be(0,
            "FAIL-CLOSED: Fabricated SubscriberId must return 0 accounts. " +
            "Even with a valid CompanyId, wrong subscriber = no data.");

        bps.Should().Be(0,
            "FAIL-CLOSED: Fabricated SubscriberId must return 0 BusinessPartners.");
    }

    [Fact(DisplayName = "ATTACK-4.5: Wrong company ID â†’ 0 accounts for company-scoped entities")]
    public async Task Attack_4_5_Wrong_company_returns_zero_accounts()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        // Attack: valid subscriber, fabricated company
        var fabricatedCompanyId = Guid.NewGuid();
        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = fabricatedCompanyId;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var accounts = await db.Accounts.CountAsync();

        accounts.Should().Be(0,
            "FAIL-CLOSED: Fabricated CompanyId (valid subscriber) must return 0 accounts. " +
            "Company-scoped filter: WHERE company_id = fabricated â†’ 0 matches.");
    }
}
