using ERP.API.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Hangfire;

public sealed class BackgroundApiHostPostgreSqlTests
{
    [Fact]
    public async Task API_serves_lightweight_readiness_without_Hangfire_worker()
    {
        await using var factory = new PostgreSqlTestWebAppFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("masterdata").And.NotContain("membership-consistency");
        factory.Services.GetService<global::Hangfire.JobStorage>().Should().BeNull();
    }
}
