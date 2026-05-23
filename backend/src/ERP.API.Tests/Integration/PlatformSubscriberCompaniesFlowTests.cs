using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Platform → Subscriber → Companies (multiempresa) + bloqueo ERP en modo global SuperAdmin.
/// </summary>
public sealed class PlatformSubscriberCompaniesFlowTests
{
    private static async Task<(IntegrationTestWebAppFactory Factory, HttpClient Client, Guid SuperAdminId)> CreatePlatformClientAsync()
    {
        var factory = new IntegrationTestWebAppFactory();
        factory.MutableSubscriber.SubscriberId = Guid.Empty;

        Guid superAdminId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await TestDataFactory.SeedCommercialPlansAndLimitsAsync(db);
            superAdminId = await TestDataFactory.SeedPlatformOperatorAsync(db, factory.MutableUser);
        }

        var token = TestJwtFactory.CreatePlatformOperatorJwt(superAdminId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (factory, client, superAdminId);
    }

    [Fact]
    public async Task PlatformSuperAdmin_creates_subscriber_with_default_company_and_membership()
    {
        var (factory, client, _) = await CreatePlatformClientAsync();
        await using var _ = factory;

        var slug = "audit-sub-" + Guid.NewGuid().ToString("N")[..8];
        var body = new
        {
            subscriberName = "Audit Subscriber",
            subscriberSlug = slug,
            adminFirstName = "Admin",
            adminLastName = "Audit",
            adminEmail = $"admin-{slug}@test.local",
            adminPassword = "SecurePass123!",
            planCode = "business",
        };

        var res = await client.PostAsJsonAsync("/api/platform/subscribers", body);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = json.GetProperty("responseObject");
        var subscriberId = Guid.Parse(data.GetProperty("subscriberId").GetString()!);
        var companyId = Guid.Parse(data.GetProperty("companyId").GetString()!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        (await db.Subscribers.AnyAsync(s => s.Id == subscriberId)).Should().BeTrue();
        (await db.Companies.IgnoreQueryFilters().CountAsync(c => c.SubscriberId == subscriberId)).Should().Be(1);
        (await db.CompanyUserMemberships.AnyAsync(m => m.CompanyId == companyId)).Should().BeTrue();
        (await db.SubscriberBillingAccounts.IgnoreQueryFilters()
            .AnyAsync(a => a.SubscriberId == subscriberId)).Should().BeTrue();
        (await db.IdentityUsers.AnyAsync(u =>
            u.Email.Value == $"admin-{slug}@test.local" && u.UserType.ToString() == "Company")).Should().BeTrue();
    }

    [Fact]
    public async Task Subscriber_admin_creates_companies_until_MAX_COMPANIES_then_403()
    {
        var (factory, platformClient, _) = await CreatePlatformClientAsync();
        await using var _ = factory;

        var slug = "multi-co-" + Guid.NewGuid().ToString("N")[..8];
        var createSub = await platformClient.PostAsJsonAsync("/api/platform/subscribers", new
        {
            subscriberName = "Multi Company Sub",
            subscriberSlug = slug,
            adminFirstName = "Co",
            adminLastName = "Admin",
            adminEmail = $"co-admin-{slug}@test.local",
            adminPassword = "SecurePass123!",
            planCode = "business",
        });
        createSub.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createSub.Content.ReadFromJsonAsync<JsonElement>();
        var responseObject = created.GetProperty("responseObject");
        var subscriberId = Guid.Parse(responseObject.GetProperty("subscriberId").GetString()!);
        var adminUserId = Guid.Parse(responseObject.GetProperty("userId").GetString()!);
        var defaultCompanyId = Guid.Parse(responseObject.GetProperty("companyId").GetString()!);

        factory.MutableSubscriber.SubscriberId = subscriberId;
        factory.MutableUser.UserId = adminUserId;
        factory.MutableCompany.CompanyId = defaultCompanyId;

        var adminToken = TestJwtFactory.CreateSessionJwt(subscriberId, adminUserId, defaultCompanyId, role: "Admin");
        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // business plan MAX_COMPANIES = 3; default company already counts as 1.
        for (var i = 2; i <= 3; i++)
        {
            var taxId = $"179{Guid.NewGuid():N}"[..13];
            var res = await adminClient.PostAsJsonAsync("/api/companies", new
            {
                taxId,
                legalName = $"Empresa {i}",
                mainAddress = "Av. Test",
                countryCode = "ECU",
                timezone = "America/Guayaquil",
                currencyCode = "USD",
            });
            res.StatusCode.Should().Be(HttpStatusCode.Created, $"company #{i} should succeed within limit");
        }

        var overTaxId = $"179{Guid.NewGuid():N}"[..13];
        var over = await adminClient.PostAsJsonAsync("/api/companies", new
        {
            taxId = overTaxId,
            legalName = "Empresa 4",
            mainAddress = "Av. Test",
            countryCode = "ECU",
            timezone = "America/Guayaquil",
            currencyCode = "USD",
        });
        over.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var overBody = await over.Content.ReadAsStringAsync();
        overBody.Should().Contain("limit", "403 body should describe commercial plan limit");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.Companies.CountAsync(c => c.SubscriberId == subscriberId)).Should().Be(3);
    }

    [Fact]
    public async Task PlatformSuperAdmin_switch_subscriber_via_auth_endpoint_returns_session_token()
    {
        var (factory, client, _) = await CreatePlatformClientAsync();
        await using var _ = factory;

        var slug = "switch-" + Guid.NewGuid().ToString("N")[..8];
        var create = await client.PostAsJsonAsync("/api/platform/subscribers", new
        {
            subscriberName = "Switch Target",
            subscriberSlug = slug,
            adminFirstName = "Ad",
            adminLastName = "Min",
            adminEmail = $"sw-{slug}@test.local",
            adminPassword = "SecurePass123!",
            planCode = "starter",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var subscriberId = Guid.Parse(created.GetProperty("responseObject").GetProperty("subscriberId").GetString()!);

        var switchRes = await client.PostAsJsonAsync("/api/auth/switch-subscriber", new { subscriberId });
        switchRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await switchRes.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        var session = body.GetProperty("responseObject");
        session.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        session.GetProperty("subscriberId").GetString().Should().Be(subscriberId.ToString());
        session.GetProperty("userType").GetString().Should().Be("Platform");
    }

    [Fact]
    public async Task GlobalPlatformSuperAdmin_cannot_access_ERP_sales_without_company_context()
    {
        var (factory, client, _) = await CreatePlatformClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/sales/invoices");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
