using ERP.Application;
using ERP.Application.Common;
using ERP.Application.Setup;
using ERP.Application.Setup.CreateInitialAdmin;
using ERP.Domain.Setup;
using ERP.Infrastructure;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Global;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace ERP.API.Tests.Support;

/// <summary>
/// WebApplicationFactory con PostgreSQL 16 real (Testcontainers) y migraciones EF.
/// </summary>
internal sealed class PostgreSqlTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly bool _useHttpCompanyContext;
    private readonly ERP.Application.Modules.Company.PhysicalEmission.IPhysicalEmissionRequirementsValidator? _physicalEmissionPolicy;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    /// <param name="physicalEmissionPolicy">
    /// Sustituye la política fiscal física solo en E2E cuyo objeto no es esa política (ver
    /// <see cref="PhysicalEmissionTestData.Satisfied"/>); null = política real.
    /// </param>
    public PostgreSqlTestWebAppFactory(
        bool useHttpCompanyContext = false,
        ERP.Application.Modules.Company.PhysicalEmission.IPhysicalEmissionRequirementsValidator? physicalEmissionPolicy = null
    )
    {
        _useHttpCompanyContext = useHttpCompanyContext;
        _physicalEmissionPolicy = physicalEmissionPolicy;
    }

    public MutableCurrentTenant MutableTenant { get; } = new();
    public MutableCurrentCompany MutableCompany { get; } = new();
    public MutableCurrentUser MutableUser { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    internal const string InitialOwnerUsername = "fixture.initial.owner";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        try { await MigrateAsync(); }
        catch
        {
            await _postgres.DisposeAsync();
            throw;
        }
    }

    public new async Task DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { await _postgres.DisposeAsync(); }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Observability:EnablePrometheus", "false");
        builder.UseSetting("Hangfire:Server:Enabled", "false");
        builder.UseSetting("Hangfire:StorageConnectionString", ConnectionString);
        builder.UseSetting("HealthChecks:EnableRedis", "false");
        builder.UseSetting("Testing:SkipCommercialPlansBootstrap", "true");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                        ["Jwt:SecretKey"] = IntegrationTestConstants.JwtSecretKey,
                        ["Jwt:Issuer"] = "ZHTechnologies",
                        ["Jwt:Audience"] = "ERPUsers",
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            foreach (
                var d in services
                    .Where(x =>
                        x.ServiceType == typeof(DbContextOptions<ErpDbContext>)
                        || x.ServiceType == typeof(ErpDbContext)
                    )
                    .ToList()
            )
                services.Remove(d);

            services.AddDbContext<ErpDbContext>(options =>
                options
                    .UseNpgsql(
                        ConnectionString,
                        b => b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)
                    )
                    // INVENTORY-ADJUSTMENTS-04 — gap encontrado durante la validación e2e: este
                    // factory reemplazaba por completo el registro de ErpDbContext (para apuntar a
                    // Postgres real vía Testcontainers) SIN volver a registrar
                    // NewChildEntityTrackingInterceptor, que en producción (ver
                    // ERP.Infrastructure/DependencyInjection.cs) corrige automáticamente entidades
                    // hijas nuevas mal clasificadas Modified en vez de Added por EF Core al
                    // descubrirlas por fixup de navegación desde un agregado ya trackeado (no
                    // recién creado). Se registra aquí para que los tests de integración corran con
                    // la misma protección que producción — el bug real de
                    // UpdateStockAdjustmentCommandHandler (StockAdjustmentId sin propagar a las
                    // líneas nuevas, ver StockAdjustmentLineResolver.ResolveAsync) se corrigió en
                    // origen porque este interceptor, por diseño, se abstiene de "arreglar" un caso
                    // con una diferencia de valores genuina (StockAdjustmentId pasaba de Guid.Empty
                    // al Id real) — solo corrige el patrón "sin ninguna diferencia real", así que no
                    // sustituye el fix de dominio, solo mejora la fidelidad de este entorno de test
                    // frente a producción para casos futuros similares. Se registra solo este
                    // interceptor (no los otros tres de DependencyInjection.cs, que dependen de
                    // servicios de sesión/tenant no relevantes para este entorno de test) para
                    // mantener el cambio acotado.
                    .AddInterceptors(
                        new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
                    )
            );

            if (_physicalEmissionPolicy is { } physicalEmissionPolicy)
            {
                foreach (
                    var d in services
                        .Where(x =>
                            x.ServiceType
                            == typeof(ERP.Application.Modules.Company.PhysicalEmission.IPhysicalEmissionRequirementsValidator)
                        )
                        .ToList()
                )
                    services.Remove(d);
                services.AddSingleton(physicalEmissionPolicy);
            }

            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentTenant)).ToList())
                services.Remove(d);
            if (!_useHttpCompanyContext)
            {
                foreach (
                    var d in services.Where(x => x.ServiceType == typeof(ICurrentCompany)).ToList()
                )
                    services.Remove(d);
            }
            foreach (var d in services.Where(x => x.ServiceType == typeof(ICurrentUser)).ToList())
                services.Remove(d);

            services.AddSingleton(MutableTenant);
            services.AddSingleton<ICurrentTenant>(sp =>
                sp.GetRequiredService<MutableCurrentTenant>()
            );
            if (!_useHttpCompanyContext)
            {
                services.AddSingleton(MutableCompany);
                services.AddSingleton<ICurrentCompany>(sp =>
                    sp.GetRequiredService<MutableCurrentCompany>()
                );
            }
            services.AddSingleton(MutableUser);
            services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<MutableCurrentUser>());

            foreach (
                var d in services
                    .Where(x =>
                        x.ImplementationType?.Name?.Contains("Kardex") == true
                        && x.ServiceType == typeof(IHostedService)
                    )
                    .ToList()
            )
                services.Remove(d);
        });
    }

    public async Task MigrateAsync()
    {
        // Never access Services here: it starts the first web host. Prepare a complete,
        // installed test ERP before either that host or a WithWebHostBuilder clone starts.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["InstallData:Enabled"] = "false",
            ["Snowflake:WorkerId"] = "17",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddSingleton<IHostApplicationLifetime, PreparationLifetime>();
        services.AddInfrastructure(config);
        services.AddApplication();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using (var migrationScope = provider.CreateAsyncScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db.Database.MigrateAsync();
            var repository = migrationScope.ServiceProvider.GetRequiredService<ISystemSetupRepository>();
            if ((await repository.GetAsync())?.IsInitialized == true)
                return;
            await migrationScope.ServiceProvider.GetRequiredService<IGlobalBootstrapOrchestrator>().RunAsync();
        }

        // Synthetic credentials belong only to this disposable database. Issue the test
        // token on the real owner session without printing/capturing the console banner.
        var token = SetupTokenCrypto.CreateRawToken();
        await using (var tokenScope = provider.CreateAsyncScope())
        {
            var repository = tokenScope.ServiceProvider.GetRequiredService<ISystemSetupRepository>();
            await tokenScope.ServiceProvider.GetRequiredService<IFirstRunSession>().RunStartupAsync(async ct =>
            {
                var state = await repository.GetAsync(ct);
                if (state is null)
                {
                    state = SystemSetupState.CreateNew();
                    await repository.AddAsync(state, ct);
                }
                state.IssueSetupToken(SetupTokenCrypto.Hash(token), DateTime.UtcNow.AddMinutes(15));
                await repository.SaveChangesAsync(ct);
            }, default);
        }
        await using var installationScope = provider.CreateAsyncScope();
        var result = await installationScope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new CreateInitialAdminCommand(InitialOwnerUsername, "Fixture", "Owner",
                "fixture.owner@example.invalid", "SyntheticFixtureInstall1!", token));
        if (!result.IsSuccess)
            throw new InvalidOperationException("Isolated test ERP installation failed.");
    }

    private sealed class PreparationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
