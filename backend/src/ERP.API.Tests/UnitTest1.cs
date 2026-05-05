using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Common;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests;

public class ApiSmokeTests
{
    private sealed class FakeTenant : ICurrentTenant
    {
        public Guid TenantId { get; init; } = Guid.NewGuid();
        public bool IsAuthenticated { get; init; } = true;
    }

    private sealed class TestAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                // Replace Npgsql DbContext with InMemory for tests
                var dbContextDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<ErpDbContext>));
                if (dbContextDescriptor is not null)
                    services.Remove(dbContextDescriptor);

                services.AddDbContext<ErpDbContext>(o =>
                    o.UseInMemoryDatabase("erp-tests"));

                // Replace tenant resolution with a deterministic fake
                var tenantDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(ICurrentTenant));
                if (tenantDescriptor is not null)
                    services.Remove(tenantDescriptor);

                services.AddScoped<ICurrentTenant>(_ => new FakeTenant());
            });
        }
    }

    [Fact]
    public async Task Swagger_should_be_available_in_development()
    {
        await using var factory = new TestAppFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/swagger/v1/swagger.json");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Access_me_permissions_should_require_authentication()
    {
        await using var factory = new TestAppFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/api/access/me/permissions");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
