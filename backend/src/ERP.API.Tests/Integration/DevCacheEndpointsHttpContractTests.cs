using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using ERP.API.Tests.Support;
using ERP.Application.Common.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-API-THIN-DEVCACHE-01 — contrato HTTP de los endpoints DEV ONLY <c>/api/dev/redis-health</c> y
/// <c>/api/dev/cache-metrics</c> (host real, sin Swagger: IgnoreApi), fijado antes de extraer la
/// sonda a ERP.API/Diagnostics/CacheHealthProbe: 404 fuera de Development y la misma forma JSON
/// con Redis ausente, disponible (contenedor real) o inalcanzable. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class DevCacheEndpointsHttpContractTests : IAsyncLifetime
{
    private static readonly string[] RedisHealthKeys =
    [
        "redisConfigured", "redisConnected", "fallbackActive", "provider",
        "writeReadOk", "latencyMs", "redisPingMs", "instanceName",
    ];

    private readonly PostgreSqlTestWebAppFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    /// <summary>Host en Development; Redis explícito (null = sin Redis: cache en memoria).</summary>
    private WebApplicationFactory<Program> Development(string? redisConnection, string? instanceName = null) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Redis:ConnectionString", redisConnection ?? "");
            builder.UseSetting("ConnectionStrings:Redis", "");
            if (instanceName is not null)
                builder.UseSetting("Redis:InstanceName", instanceName);
        });

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Get(WebApplicationFactory<Program> app, string path)
    {
        var response = await app.CreateClient().GetAsync(path);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        return (response.StatusCode, body);
    }

    private static JsonElement RedisHealthData(JsonElement body)
    {
        var data = body.GetProperty("data");
        data.EnumerateObject().Select(p => p.Name).Should().Equal(RedisHealthKeys, "misma forma y orden del JSON");
        return data;
    }

    [Theory]
    [InlineData("/api/dev/redis-health")]
    [InlineData("/api/dev/cache-metrics")]
    public async Task Fuera_de_Development_responde_404(string path)
    {
        var (status, body) = await Get(_factory, path);

        status.Should().Be(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().Should().Be("NOT_FOUND");
        body.GetProperty("data").GetProperty("errors").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Endpoint disponible solo en Development.");
    }

    [Fact]
    public async Task Sin_Redis_configurado_informa_no_configurado_y_hace_round_trip_en_memoria()
    {
        await using var app = Development(redisConnection: null);
        var provider = app.Services.GetRequiredService<ICacheProviderStatus>();

        var (status, body) = await Get(app, "/api/dev/redis-health");

        status.Should().Be(HttpStatusCode.OK);
        var data = RedisHealthData(body);
        data.GetProperty("redisConfigured").GetBoolean().Should().BeFalse();
        data.GetProperty("redisConnected").GetBoolean().Should().BeFalse();
        data.GetProperty("fallbackActive").GetBoolean().Should().Be(provider.FallbackActive);
        data.GetProperty("provider").GetString().Should().Be(provider.ProviderName);
        data.GetProperty("writeReadOk").GetBoolean().Should().BeTrue("round-trip sobre IDistributedCache en memoria");
        data.GetProperty("latencyMs").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("redisPingMs").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("instanceName").GetString().Should().Be("ERP_");
    }

    [Fact]
    public async Task Con_Redis_disponible_conecta_hace_ping_y_round_trip()
    {
        await using IContainer redis = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();
        await redis.StartAsync();
        var connection = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}";

        await using var app = Development(connection, instanceName: "ERP_TEST_");
        var provider = app.Services.GetRequiredService<ICacheProviderStatus>();

        var (status, body) = await Get(app, "/api/dev/redis-health");

        status.Should().Be(HttpStatusCode.OK);
        var data = RedisHealthData(body);
        data.GetProperty("redisConfigured").GetBoolean().Should().BeTrue();
        data.GetProperty("redisConnected").GetBoolean().Should().BeTrue();
        data.GetProperty("redisPingMs").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("writeReadOk").GetBoolean().Should().BeTrue();
        data.GetProperty("latencyMs").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("fallbackActive").GetBoolean().Should().Be(provider.FallbackActive);
        data.GetProperty("provider").GetString().Should().Be(provider.ProviderName);
        data.GetProperty("instanceName").GetString().Should().Be("ERP_TEST_");
    }

    [Fact]
    public async Task Con_Redis_inalcanzable_informa_desconectado_sin_ping_y_responde_200()
    {
        await using var app = Development("127.0.0.1:1,connectTimeout=500,abortConnect=true");

        var (status, body) = await Get(app, "/api/dev/redis-health");

        status.Should().Be(HttpStatusCode.OK);
        var data = RedisHealthData(body);
        data.GetProperty("redisConfigured").GetBoolean().Should().BeTrue();
        data.GetProperty("redisConnected").GetBoolean().Should().BeFalse();
        data.GetProperty("redisPingMs").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("writeReadOk").GetBoolean().Should().BeFalse("el round-trip contra Redis caído falla y se informa, no revienta");
        data.GetProperty("latencyMs").GetInt64().Should().Be(-1);
    }

    [Fact]
    public async Task Cache_metrics_en_Development_mantiene_su_forma()
    {
        await using var app = Development(redisConnection: null);

        var (status, body) = await Get(app, "/api/dev/cache-metrics");

        status.Should().Be(HttpStatusCode.OK);
        var data = body.GetProperty("data");
        data.EnumerateObject().Select(p => p.Name).Should().Equal(
            "cache_hit_total", "cache_miss_total", "cache_set_total", "hitRatio", "hitsByCategory",
            "missesByCategory", "permissions", "provider", "fallbackActive", "redisConfigured");
        data.GetProperty("permissions").EnumerateObject().Select(p => p.Name).Should().Equal(
            "cache_hit_total", "cache_miss_total", "cache_set_total", "cache_error_total", "hitRatio", "miss_reason");
        data.GetProperty("redisConfigured").GetBoolean().Should().BeFalse();
    }
}
