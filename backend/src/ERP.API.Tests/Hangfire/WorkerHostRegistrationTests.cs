using ERP.API.Hangfire;
using ERP.API.Hosting;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ERP.API.Tests.Hangfire;

/// <summary>
/// API y worker componen la infraestructura común solo vía <see cref="ErpHostRegistration"/>:
/// misma caché (Redis o memoria según la misma configuración) y mismas opciones.
/// </summary>
public sealed class WorkerHostRegistrationTests
{
    private const string Storage = "Host=localhost;Database=worker_registration;Username=erp";

    [Theory]
    [InlineData(null, "Memory")]
    [InlineData("localhost:6390", "Redis")]
    public void Worker_resolves_the_same_cache_provider_and_options_as_the_API(string? redis, string expected)
    {
        var args = WorkerArgs(redis);
        using var worker = BackgroundWorkerHost.Build(args);

        var apiBuilder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(
            new Microsoft.AspNetCore.Builder.WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ContentRootPath = ContentRoot(),
                Args = args,
            }
        );
        apiBuilder.Configuration.AddErpConfigurationSources(apiBuilder.Environment, args);
        apiBuilder.Services.AddErpHostCore(apiBuilder.Configuration, apiBuilder.Environment);
        using var api = apiBuilder.Services.BuildServiceProvider();

        foreach (var services in new IServiceProvider[] { worker.Services, api })
        {
            services.GetRequiredService<ICacheProviderStatus>().ProviderName.Should().Be(expected);
            services.GetRequiredService<IDistributedCache>().Should().NotBeNull();
            services.GetRequiredService<IOptions<SessionExpirationOptions>>().Value.Should().NotBeNull();
            services.GetRequiredService<SessionExpirationOptions>().Should().NotBeNull();
            services.GetRequiredService<IOptions<SriPollingOptions>>().Value.Should().NotBeNull();
        }
    }

    private static string[] WorkerArgs(string? redis)
    {
        var args = new List<string>
        {
            "--contentRoot", ContentRoot(), "--environment", "Testing",
            "--Hangfire:Server:Enabled", "true", "--Hangfire:StorageConnectionString", Storage,
            "--ConnectionStrings:DefaultConnection", Storage,
        };
        if (redis is not null)
            args.AddRange(["--Redis:ConnectionString", redis]);
        return args.ToArray();
    }

    private static string ContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "STATUS.md")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "backend/src/ERP.API");
    }
}
