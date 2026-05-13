using System.Net;
using FluentAssertions;
using ERP.API.Tests.Support;

namespace ERP.API.Tests.Integration;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task Health_live_returns_200()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient();
        var res = await client.GetAsync("/health/live");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Health_ready_returns_200_with_in_memory_database()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient();
        var res = await client.GetAsync("/health/ready");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("database");
    }
}
