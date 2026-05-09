using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>Pruebas HTTP con WebApplicationFactory y JWT de sesión simulado (Paso 8.3).</summary>
public sealed class AuthenticatedApiTests
{
    [Fact]
    public async Task Gastos_Get_con_bearer_token_admin_responde_200()
    {
        await using var factory = new IntegrationTestWebAppFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
            factory.MutableTenant.TenantId = seed.TenantId;
            factory.MutableUser.UserId     = seed.UserId;
        }

        var token = TestJwtFactory.CreateSessionJwt(factory.MutableTenant.TenantId, factory.MutableUser.UserId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/gastos");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Compras_Get_con_bearer_token_admin_responde_200()
    {
        await using var factory = new IntegrationTestWebAppFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
            factory.MutableTenant.TenantId = seed.TenantId;
            factory.MutableUser.UserId     = seed.UserId;
        }

        var token = TestJwtFactory.CreateSessionJwt(factory.MutableTenant.TenantId, factory.MutableUser.UserId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/compras");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
