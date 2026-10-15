using ERP.Application;
using ERP.Application.Common.Config;
using ERP.Infrastructure;
using ERP.Infrastructure.Caching;
using Microsoft.AspNetCore.DataProtection;
using Serilog;

namespace ERP.API.Hosting;

/// <summary>
/// Registro común de los dos hosts del mismo ejecutable — API HTTP y worker (<c>--worker</c>,
/// ADR-043): fuentes de configuración, logging, caché distribuida, Infrastructure/Application,
/// Data Protection y opciones. Lo exclusivo de HTTP (controllers, JWT, CORS, rate limit, health,
/// autorización) vive solo en <c>Program.cs</c>. Ningún host compone estos servicios por su cuenta.
/// </summary>
public static class ErpHostRegistration
{
    /// <summary>
    /// Precedencia estándar: appsettings &lt; user-secrets &lt; environment variables &lt; command line
    /// (user-secrets también fuera de Development; nunca en Testing). Alias de hosting
    /// <c>DB_CONNECTION_STRING</c> → <c>ConnectionStrings:DefaultConnection</c>.
    /// </summary>
    public static void AddErpConfigurationSources(
        this IConfigurationManager configuration,
        IHostEnvironment environment,
        string[] args
    )
    {
        if (!environment.IsEnvironment("Testing"))
            configuration.AddUserSecrets(typeof(ErpHostRegistration).Assembly, optional: true);
        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(args);

        var dbFromEnv = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(dbFromEnv))
            configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", dbFromEnv),
            ]);
    }

    /// <summary>Redis si <c>Redis:ConnectionString</c> o <c>ConnectionStrings:Redis</c> está definida.</summary>
    public static string? RedisConnection(IConfiguration configuration)
    {
        var connection =
            configuration["Redis:ConnectionString"] ?? configuration.GetConnectionString("Redis");
        return string.IsNullOrWhiteSpace(connection) ? null : connection;
    }

    public static IServiceCollection AddErpHostCore(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.AddSerilog(
            (_, logger) =>
                logger
                    .ReadFrom.Configuration(configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "ERP")
        );

        // IDistributedCache: Redis si está configurado; si no, memoria (no comparte entre
        // instancias). API y worker resuelven la misma caché, así que comparten invalidaciones.
        var redisConnection = RedisConnection(configuration);
        if (redisConnection is not null)
        {
            var redisInstanceName = configuration["Redis:InstanceName"];
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = string.IsNullOrWhiteSpace(redisInstanceName)
                    ? "ERP_"
                    : redisInstanceName;
            });

            // Una sola conexión Redis: la usa RedisCache y la usan los rate limits que necesitan
            // incremento atómico (DistributedFixedWindowRateLimit), que IDistributedCache no ofrece.
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection)
            );
            services
                .AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
                .Configure<IServiceProvider>(
                    (options, sp) =>
                        options.ConnectionMultiplexerFactory = () =>
                            Task.FromResult(
                                sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>()
                            )
                );
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddDistributedCacheInstrumentation(configuration, redisConnection is not null);

        services.AddInfrastructure(configuration);

        // Sin persistencia explícita, el keyring de Data Protection es efímero al contenedor y se
        // pierde en cada recreate — invalidando secretos ya cifrados con él (p.ej.
        // sri_settings.cert_password). Se persiste en el mismo volumen durable que FileStorage
        // (erp-api-files) para sobrevivir recreates; API y worker leen el mismo keyring.
        var dataProtectionKeysPath = Path.Combine(
            configuration["FileStorage:BasePath"]
                ?? Path.Combine(environment.ContentRootPath, "files"),
            "dataprotection-keys"
        );
        services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

        services.AddApplication();

        // Opciones: registradas como IOptions<> (convención .NET) y, donde los handlers de
        // Application las inyectan directamente, también como plain class.
        var kardexSection = configuration.GetSection(KardexOptions.Section);
        services.Configure<KardexOptions>(kardexSection);
        services.AddSingleton(_ => kardexSection.Get<KardexOptions>() ?? new KardexOptions());
        services.Configure<PasswordResetOptions>(
            configuration.GetSection(PasswordResetOptions.SectionName)
        );
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));
        services.Configure<SriPollingOptions>(configuration.GetSection(SriPollingOptions.Section));

        // Política de expiración de UserSession (Fase 9) — plain class inyectable directamente
        // en ExpireUserSessionsHandler (Application).
        var sessionExpirationSection = configuration.GetSection(SessionExpirationOptions.Section);
        services.Configure<SessionExpirationOptions>(sessionExpirationSection);
        services.AddSingleton(_ =>
            sessionExpirationSection.Get<SessionExpirationOptions>()
            ?? new SessionExpirationOptions()
        );

        return services;
    }
}
