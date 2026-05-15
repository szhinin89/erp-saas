using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Support;

/// <summary>API real con EF InMemory, suscripciÃ³n ilimitada y tenant/usuario mutables para integraciÃ³n.</summary>
internal sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>
{
    public MutableCurrentTenant MutableTenant { get; } = new();
    public MutableCurrentUser   MutableUser   { get; } = new();
    public bool UseScalableMode { get; set; } = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ContentRoot = proyecto ERP.API para que exista Templates/ (RazorLight) al ejecutar tests desde ERP.API.Tests/bin.
        var apiProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ERP.API"));
        if (Directory.Exists(Path.Combine(apiProjectRoot, "Templates")))
            builder.UseContentRoot(apiProjectRoot);

        builder.UseEnvironment("Development");

        // Gana a user-secrets / appsettings para que el JWT de prueba coincida con la validaciÃ³n del host.
        builder.UseSetting("Jwt:SecretKey", IntegrationTestConstants.JwtSecretKey);
        builder.UseSetting("Jwt:Issuer", "ZHTechnologies");
        builder.UseSetting("Jwt:Audience", "ERPUsers");
        builder.UseSetting("Jwt:ExpirationMinutes", "60");
        builder.UseSetting("HealthChecks:EnableRedis", "false");
        builder.UseSetting("HealthChecks:SriProbeUrl", "");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=unused",
                ["ConnectionStrings:Redis"] = "",
                ["Redis:ConnectionString"] = "",
                ["HealthChecks:EnableRedis"] = "false",
                ["HealthChecks:SriProbeUrl"] = "",
                ["Jwt:SecretKey"]                      = IntegrationTestConstants.JwtSecretKey,
                ["Jwt:Issuer"]                         = "ZHTechnologies",
                ["Jwt:Audience"]                       = "ERPUsers",
                ["Jwt:ExpirationMinutes"]              = "60",
                ["Kardex:UseScalableMode"]             = UseScalableMode.ToString(),
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Quitar el registro Npgsql de AddInfrastructure y sustituir por InMemory sin mezclar proveedores internos.
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
            services.AddDbContext<ErpDbContext>((sp, opts) =>
            {
                opts.UseInMemoryDatabase(dbName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentTenant)).ToList())
                services.Remove(d);
            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentUser)).ToList())
                services.Remove(d);

            services.AddSingleton(MutableTenant);
            services.AddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<MutableCurrentTenant>());
            services.AddSingleton(MutableUser);
            services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<MutableCurrentUser>());

            foreach (var d in services.Where(x => x.ServiceType == typeof(IFileStorage)).ToList())
                services.Remove(d);
            services.AddSingleton<IFileStorage, NoOpFileStorage>();

            foreach (var d in services.Where(x => x.ServiceType == typeof(ISubscriptionService)).ToList())
                services.Remove(d);
            services.AddSingleton<ISubscriptionService, AllowAllSubscriptionService>();
        });
    }
}

