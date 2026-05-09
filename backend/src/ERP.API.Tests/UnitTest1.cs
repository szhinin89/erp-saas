using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ERP.API.Tests.Support;

namespace ERP.API.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task Swagger_should_be_available_in_development()
    {
        await using var factory = new IntegrationTestWebAppFactory();
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
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/api/access/me/permissions");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
