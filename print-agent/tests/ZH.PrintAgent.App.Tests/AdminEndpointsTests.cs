using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ZH.PrintAgent.App.Tests;

public sealed class AdminEndpointsTests
{
    private const string ApiKeyHeader = "X-ZH-PrintAgent-Key";

    [Fact]
    public async Task Admin_status_is_reachable_without_key_before_setup_on_loopback()
    {
        using var factory = CreateFactory(setupCompleted: false, allowLan: false, bindHost: "127.0.0.1");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/status");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Admin_status_requires_key_once_setup_is_completed()
    {
        using var factory = CreateFactory(setupCompleted: true, allowLan: false, bindHost: "127.0.0.1");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/status");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_status_requires_key_when_lan_is_allowed_even_before_setup()
    {
        using var factory = CreateFactory(setupCompleted: false, allowLan: true, bindHost: "0.0.0.0");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/status");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Print_jobs_route_always_requires_key_regardless_of_setup_state()
    {
        using var factory = CreateFactory(setupCompleted: false, allowLan: false, bindHost: "127.0.0.1");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/print-jobs");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Print_jobs_route_succeeds_with_valid_key()
    {
        const string apiKey = "test-only-key-0123456789";
        using var factory = CreateFactory(setupCompleted: true, allowLan: false, bindHost: "127.0.0.1", apiKey: apiKey);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyHeader, apiKey);

        var response = await client.GetAsync("/print-jobs");

        response.EnsureSuccessStatusCode();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        bool setupCompleted,
        bool allowLan,
        string bindHost,
        string apiKey = "test-only-key-0123456789")
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "zh-print-agent-admin-tests", Guid.NewGuid().ToString("N"));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PrintAgent:DataDirectory"] = dataDirectory,
                    ["PrintAgent:BindHost"] = bindHost,
                    ["PrintAgent:AllowLan"] = allowLan.ToString(),
                    ["PrintAgent:SetupCompleted"] = setupCompleted.ToString(),
                    ["PrintAgent:ApiKey"] = apiKey
                });
            });
        });
    }
}
