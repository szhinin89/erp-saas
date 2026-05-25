using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Infrastructure.BackgroundServices;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Support;

/// <summary>API real con EF InMemory, suscripción/entitlements ilimitados y tenant/usuario/company mutables.</summary>
internal sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>
{
    public MutableCurrentSubscriber MutableSubscriber { get; } = new();
    public MutableCurrentUser MutableUser { get; } = new();
    public MutableCurrentCompany MutableCompany { get; } = new();
    public bool UseScalableMode { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var apiProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ERP.API"));
        if (Directory.Exists(Path.Combine(apiProjectRoot, "Templates")))
            builder.UseContentRoot(apiProjectRoot);

        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:SecretKey", IntegrationTestConstants.JwtSecretKey);
        builder.UseSetting("Jwt:Issuer", "ZHTechnologies");
        builder.UseSetting("Jwt:Audience", "ERPUsers");
        builder.UseSetting("Jwt:ExpirationMinutes", "60");
        builder.UseSetting("HealthChecks:EnableRedis", "false");
        builder.UseSetting("HealthChecks:SriProbeUrl", "");
        builder.UseSetting("Hangfire:Enabled", "false");
        builder.UseSetting("InstallData:Enabled", "false");
        builder.UseSetting("Development:SeedDemoSubscriber", "false");
        builder.UseSetting("Development:SyncFuncionalidadesOnStartup", "false");
        builder.UseSetting("Testing:SkipFirstRunSetup", "true");
        builder.UseSetting("Testing:SkipCommercialPlansBootstrap", "true");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=unused",
                ["ConnectionStrings:Redis"] = "",
                ["Redis:ConnectionString"] = "",
                ["HealthChecks:EnableRedis"] = "false",
                ["HealthChecks:SriProbeUrl"] = "",
                ["Jwt:SecretKey"] = IntegrationTestConstants.JwtSecretKey,
                ["Jwt:Issuer"] = "ZHTechnologies",
                ["Jwt:Audience"] = "ERPUsers",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Hangfire:Enabled"] = "false",
                ["InstallData:Enabled"] = "false",
                ["Development:SeedDemoSubscriber"] = "false",
                ["Development:SyncFuncionalidadesOnStartup"] = "false",
                ["Testing:SkipFirstRunSetup"] = "true",
                ["Testing:SkipCommercialPlansBootstrap"] = "true",
                ["Kardex:UseScalableMode"] = UseScalableMode.ToString(),
            });
        });

        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(x =>
                         x.ServiceType == typeof(DbContextOptions<ErpDbContext>)
                         || x.ServiceType == typeof(ErpDbContext)
                         || (x.ServiceType.IsGenericType
                             && x.ServiceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1"
                             && x.ServiceType.GenericTypeArguments[0] == typeof(ErpDbContext)))
                     .ToList())
            {
                services.Remove(d);
            }

            var dbName = "erp-api-integration-" + Guid.NewGuid().ToString("N");
            services.AddDbContext<ErpDbContext>((_, opts) =>
            {
                opts.UseInMemoryDatabase(dbName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentSubscriber)).ToList())
                services.Remove(d);
            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentUser)).ToList())
                services.Remove(d);
            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentCompany)).ToList())
                services.Remove(d);

            services.AddSingleton(MutableSubscriber);
            services.AddSingleton<ICurrentSubscriber>(sp => sp.GetRequiredService<MutableCurrentSubscriber>());
            services.AddSingleton(MutableUser);
            services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<MutableCurrentUser>());
            services.AddSingleton(MutableCompany);
            services.AddSingleton<ICurrentCompany>(sp => sp.GetRequiredService<MutableCurrentCompany>());

            foreach (var d in services.Where(x => x.ServiceType == typeof(IFileStorage)).ToList())
                services.Remove(d);
            services.AddSingleton<IFileStorage, NoOpFileStorage>();

            foreach (var d in services.Where(x => x.ServiceType == typeof(ISubscriptionService)).ToList())
                services.Remove(d);
            services.AddSingleton<ISubscriptionService, AllowAllSubscriptionService>();

            foreach (var d in services.Where(x => x.ServiceType == typeof(ISubscriberEntitlementsService)).ToList())
                services.Remove(d);
            services.AddSingleton<ISubscriberEntitlementsService, AllowAllEntitlementsService>();

            foreach (var d in services
                         .Where(x => x.ServiceType == typeof(IHostedService)
                                     && (x.ImplementationType == typeof(KardexSnapshotWorker)
                                         || x.ImplementationType == typeof(KardexReportProcessor)))
                         .ToList())
            {
                services.Remove(d);
            }
        });
    }
}
