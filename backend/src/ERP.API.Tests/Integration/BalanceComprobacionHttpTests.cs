using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class BalanceComprobacionHttpTests
{
    private static async Task<(IntegrationTestWebAppFactory Factory, HttpClient Client, IntegrationSeedData.SeedResult Seed)>
        CreateClientAsync()
    {
        var factory = new IntegrationTestWebAppFactory();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var token = TestJwtFactory.CreateSessionJwt(seed.SubscriberId, seed.UserId, seed.CompanyId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return (factory, client, seed);
    }

    [Fact]
    public async Task BalanceComprobacion_sin_token_responde_401()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync("/api/finance/accounts/balance-comprobacion?desde=2026-01-01&hasta=2026-05-22");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BalanceComprobacion_con_fechas_validas_responde_200()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/finance/accounts/balance-comprobacion?desde=2026-01-01&hasta=2026-05-22");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        body.Should().Contain("\"success\":true");
    }
}
