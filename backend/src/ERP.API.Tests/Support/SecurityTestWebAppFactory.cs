using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Infrastructure.BackgroundServices;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ERP.API.Tests.Support;

/// <summary>
/// Factory para tests de seguridad multiempresa real.
///
/// A diferencia de <see cref="IntegrationTestWebAppFactory"/>, esta factory NO reemplaza
/// ICurrentSubscriber / ICurrentCompany / ICurrentUser con Mutables.
/// Los servicios de contexto actuales leen desde el HttpContext (claims del JWT),
/// permitiendo testear aislamiento real entre companies y subscribers.
///
/// Usar con <see cref="TestJwtFactory"/> para generar JWTs con claims específicos
/// y verificar que el sistema aplica correctamente los filtros de seguridad.
/// </summary>
internal sealed class SecurityTestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "erp-security-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:SecretKey",          IntegrationTestConstants.JwtSecretKey);
        builder.UseSetting("Jwt:Issuer",             "ZHTechnologies");
        builder.UseSetting("Jwt:Audience",           "ERPUsers");
        builder.UseSetting("Jwt:ExpirationMinutes",  "60");
        builder.UseSetting("HealthChecks:EnableRedis", "false");
        builder.UseSetting("HealthChecks:SriProbeUrl", "");
        builder.UseSetting("Hangfire:Enabled",       "false");
        builder.UseSetting("InstallData:Enabled",    "false");
        builder.UseSetting("Development:SeedDemoTenant",              "false");
        builder.UseSetting("Development:SyncFuncionalidadesOnStartup","false");
        builder.UseSetting("Testing:SkipFirstRunSetup",               "true");
        builder.UseSetting("Testing:SkipCommercialPlansBootstrap",    "true");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=unused",
                ["ConnectionStrings:Redis"]             = "",
                ["Redis:ConnectionString"]              = "",
                ["HealthChecks:EnableRedis"]            = "false",
                ["HealthChecks:SriProbeUrl"]            = "",
                ["Jwt:SecretKey"]                       = IntegrationTestConstants.JwtSecretKey,
                ["Jwt:Issuer"]                          = "ZHTechnologies",
                ["Jwt:Audience"]                        = "ERPUsers",
                ["Jwt:ExpirationMinutes"]               = "60",
                ["Hangfire:Enabled"]                    = "false",
                ["InstallData:Enabled"]                 = "false",
                ["Development:SeedDemoTenant"]          = "false",
                ["Development:SyncFuncionalidadesOnStartup"] = "false",
                ["Testing:SkipFirstRunSetup"]            = "true",
                ["Testing:SkipCommercialPlansBootstrap"] = "true",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Reemplazar EF con InMemory — PERO mantener los Current* services reales
            // (ICurrentSubscriber, ICurrentCompany, ICurrentUser leen del JWT real)
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

            services.AddDbContext<ErpDbContext>((_, opts) =>
            {
                opts.UseInMemoryDatabase(_dbName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

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

    /// <summary>
    /// Crea un scope de servicio con el DB poblado.
    /// El caller debe hacer Dispose del scope.
    /// </summary>
    public IServiceScope CreateDbScope() => Services.CreateScope();

    /// <summary>
    /// Crea un HttpClient con el JWT de sesión indicado como Authorization header.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string jwt)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }
}
