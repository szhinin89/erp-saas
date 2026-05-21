using System.Net.Http.Headers;
using ERP.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Support;

/// <summary>Cliente HTTP autenticado con contexto subscriber + company para runtime ERP.</summary>
internal sealed class TestAuthClient : IAsyncDisposable
{
    public IntegrationTestWebAppFactory Factory { get; }
    public HttpClient Client { get; }
    public IntegrationSeedData.SeedResult Seed { get; }

    private TestAuthClient(
        IntegrationTestWebAppFactory factory,
        HttpClient client,
        IntegrationSeedData.SeedResult seed)
    {
        Factory = factory;
        Client = client;
        Seed = seed;
    }

    /// <summary>Seed mínimo + JWT session con company_id (runtime operativo).</summary>
    public static async Task<TestAuthClient> CreateRuntimeAsync(
        IntegrationTestWebAppFactory? factory = null,
        Action<ErpDbContext, IntegrationSeedData.SeedResult>? afterSeed = null,
        CancellationToken ct = default)
    {
        factory ??= new IntegrationTestWebAppFactory();

        IntegrationSeedData.SeedResult seed;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            seed = await TestDataFactory.SeedSubscriberDemoAsync(db, factory, ct);
            afterSeed?.Invoke(db, seed);
        }

        EnsureCompanyContext(factory, seed);

        var token = TestJwtFactory.CreateSessionJwt(seed.SubscriberId, seed.UserId, seed.CompanyId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new TestAuthClient(factory, client, seed);
    }

    public static void EnsureCompanyContext(IntegrationTestWebAppFactory factory, IntegrationSeedData.SeedResult seed)
    {
        factory.MutableSubscriber.SubscriberId = seed.SubscriberId;
        factory.MutableUser.UserId = seed.UserId;
        factory.MutableCompany.CompanyId = seed.CompanyId;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
