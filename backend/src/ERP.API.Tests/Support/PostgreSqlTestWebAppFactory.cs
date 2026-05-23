using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Infrastructure.BackgroundServices;
using ERP.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ERP.API.Tests.Support;

/// <summary>
/// WebApplicationFactory con PostgreSQL 16 real (Testcontainers) y migraciones EF.
/// </summary>
internal sealed class PostgreSqlTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    public MutableCurrentSubscriber MutableSubscriber { get; } = new();
    public MutableCurrentCompany MutableCompany { get; } = new();
    public MutableCurrentUser MutableUser { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Observability:EnablePrometheus", "false");
        builder.UseSetting("Hangfire:Enabled", "false");
        builder.UseSetting("HealthChecks:EnableRedis", "false");
        builder.UseSetting("Testing:SkipFirstRunSetup", "true");
        builder.UseSetting("Testing:SkipCommercialPlansBootstrap", "true");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Jwt:SecretKey"] = IntegrationTestConstants.JwtSecretKey,
                ["Jwt:Issuer"] = "ZHTechnologies",
                ["Jwt:Audience"] = "ERPUsers",
            });
        });

        builder.ConfigureServices(services =>
        {
            foreach (var d in services.Where(x =>
                         x.ServiceType == typeof(DbContextOptions<ErpDbContext>)
                         || x.ServiceType == typeof(ErpDbContext)).ToList())
                services.Remove(d);

            services.AddDbContext<ErpDbContext>(options =>
                options.UseNpgsql(ConnectionString, b =>
                    b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)));

            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentSubscriber)).ToList())
                services.Remove(d);
            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentCompany)).ToList())
                services.Remove(d);
            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentUser)).ToList())
                services.Remove(d);

            services.AddSingleton(MutableSubscriber);
            services.AddSingleton<ICurrentSubscriber>(sp => sp.GetRequiredService<MutableCurrentSubscriber>());
            services.AddSingleton(MutableCompany);
            services.AddSingleton<ICurrentCompany>(sp => sp.GetRequiredService<MutableCurrentCompany>());
            services.AddSingleton(MutableUser);
            services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<MutableCurrentUser>());

            services.AddSingleton<ISubscriptionService, AllowAllSubscriptionService>();
            services.AddSingleton<ISubscriberEntitlementsService, AllowAllEntitlementsService>();

            foreach (var d in services.Where(x => x.ImplementationType?.Name?.Contains("Kardex") == true
                                                  && x.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(d);
        });
    }

    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();
    }
}
