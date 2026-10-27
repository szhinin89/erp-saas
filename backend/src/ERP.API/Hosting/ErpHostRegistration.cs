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
    /// Fuentes únicas de API, worker y herramientas EF (<see cref="ErpDesignTimeDbContextFactory"/>):
    /// appsettings &lt; user-secrets (solo Development) &lt; environment variables &lt; command line.
    /// Fuera de Development la conexión llega solo del entorno (<c>ConnectionStrings__DefaultConnection</c>),
    /// nunca de los secretos locales del desarrollador.
    /// </summary>
    public static IConfigurationBuilder AddErpConfigurationSources(
        this IConfigurationBuilder configuration,
        string environmentName,
        string[] args
    )
    {
        if (string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
            configuration.AddUserSecrets(typeof(ErpHostRegistration).Assembly, optional: true);
        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(args);
        return configuration;
    }

    /// <summary>Redis si <c>Redis:ConnectionString</c> o <c>ConnectionStrings:Redis</c> está definida.</summary>
    public static string? RedisConnection(IConfiguration configuration)
    {
        var connection =
            configuration["Redis:ConnectionString"] ?? configuration.GetConnectionString("Redis");
        return string.IsNullOrWhiteSpace(connection) ? null : connection;
    }

    /// <summary>
    /// SEC-06: con varias instancias de API, Redis es obligatorio — la caché en memoria (versiones e
    /// invalidaciones de permisos incluidas) no se comparte entre procesos y solo se admite en un
    /// despliegue de instancia única. Un despliegue multi-instancia declara <c>Redis:Required=true</c>
    /// y el host no arranca sin conexión Redis.
    /// </summary>
    public static void EnsureRedisWhenRequired(IConfiguration configuration, string? redisConnection)
    {
        if (redisConnection is null && configuration.GetValue<bool>("Redis:Required"))
            throw new InvalidOperationException(
                "Redis:Required=true (despliegue con varias instancias de API) pero no hay "
                    + "Redis:ConnectionString ni ConnectionStrings:Redis. La caché en memoria solo se "
                    + "admite en un despliegue de instancia única."
            );
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
        EnsureRedisWhenRequired(configuration, redisConnection);
        if (redisConnection is not null)
        {
            var redisInstanceName = configuration["Redis:InstanceName"];
            // SEC-06: el host no arranca sin un Redis conforme y verificable.
            services.AddHostedService<PermissionsCacheRedisStartupValidator>();

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
