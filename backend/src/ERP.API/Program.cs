// CA1848/CA1873: Program.cs top-level statements cannot use [LoggerMessage] source generators
#pragma warning disable CA1848, CA1873

using ERP.API.Authorization;
using ERP.API.Extensions;
using ERP.API.Hangfire;
using ERP.API.Health;
using ERP.API.Middleware;
using ERP.API.Services;
using ERP.Application;
using ERP.Infrastructure;
using ERP.Infrastructure.Caching;
using ERP.Infrastructure.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using QuestPDF.Infrastructure;
using Serilog;
using System.Threading.RateLimiting;

// Licencia Community: libre para proyectos con ingresos anuales < 1 M USD.
// Cambiar a LicenseType.Professional si aplica.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Asegura que los user-secrets del API ganen a appsettings.
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

// Alias opcional en hosting: DB_CONNECTION_STRING → ConnectionStrings:DefaultConnection
var dbFromEnv = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(dbFromEnv))
{
    builder.Configuration.AddInMemoryCollection([
        new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", dbFromEnv),
    ]);
}

// Production guard: falla rápido si sigue el placeholder de appsettings.json en vez
// de un valor real inyectado por env/secret store. Nunca loggear el valor, solo el
// nombre de la variable faltante.
if (builder.Environment.IsProduction())
{
    var jwtSecret = builder.Configuration["Jwt:SecretKey"];
    if (
        string.IsNullOrWhiteSpace(jwtSecret)
        || jwtSecret == "CHANGE_ME_USE_ENV_VAR_OR_USER_SECRETS"
    )
    {
        throw new InvalidOperationException(
            "Production requires Jwt:SecretKey configured via environment variable or secret store."
        );
    }

    var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
    if (
        string.IsNullOrWhiteSpace(defaultConnection)
        || defaultConnection.Contains("Password=CHANGE_ME", StringComparison.Ordinal)
    )
    {
        throw new InvalidOperationException(
            "Production requires ConnectionStrings:DefaultConnection configured via environment variable or secret store."
        );
    }

    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (corsOrigins is null || corsOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "Production requires Cors:AllowedOrigins configured — no silent fallback to localhost."
        );
    }

    var passwordResetBaseUrl = builder.Configuration["PasswordReset:PublicBaseUrl"];
    if (
        string.IsNullOrWhiteSpace(passwordResetBaseUrl)
        || passwordResetBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || passwordResetBaseUrl.Contains("127.0.0.1", StringComparison.Ordinal)
    )
    {
        throw new InvalidOperationException(
            "Production requires PasswordReset:PublicBaseUrl configured to a real public URL — no silent fallback to localhost."
        );
    }
}

builder.Host.UseSerilog(
    (context, _, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "ERP");
    }
);

builder
    .Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            // Fallback solo alcanzable en Development/Testing — el guard de Production
            // arriba ya lanzó si Cors:AllowedOrigins está vacío/ausente.
            var origins =
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:5173"];

            policy
                .WithOrigins(origins)
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .WithHeaders(
                    "Authorization",
                    "Content-Type",
                    "Accept",
                    "x-correlation-id",
                    "X-Company-Id",
                    "x-company-session-version",
                    "X-Branch-Id"
                )
                .AllowCredentials();
        }
    );
});

builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        "per-tenant",
        httpContext =>
        {
            var tenantId = httpContext.User.FindFirst("tenant_id")?.Value ?? "anonymous";
            return RateLimitPartition.GetFixedWindowLimiter(
                tenantId,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 600,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }
            );
        }
    );

    var refreshIpLimit = builder.Configuration.GetValue("Auth:RefreshRateLimitPerIpPerMinute", 60);
    options.AddPolicy(
        "auth-refresh-ip",
        httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                ip,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = refreshIpLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }
            );
        }
    );
});

// IDistributedCache: Redis si Redis:ConnectionString o ConnectionStrings:Redis está definida;
// en tests y producción sin Redis, memoria (no comparte entre instancias).
var redisConnection =
    builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Redis");
var redisInstanceName = builder.Configuration["Redis:InstanceName"];
if (string.IsNullOrWhiteSpace(redisInstanceName))
    redisInstanceName = "ERP_";

var redisConfigured = !string.IsNullOrWhiteSpace(redisConnection);
if (redisConfigured)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = redisInstanceName;
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddDistributedCacheInstrumentation(builder.Configuration, redisConfigured);

builder.Services.AddInfrastructure(builder.Configuration);

// Sin persistencia explícita, el keyring de Data Protection es efímero al contenedor y se
// pierde en cada recreate — invalidando secretos ya cifrados con él (p.ej. sri_settings.cert_password).
// Se persiste en el mismo volumen durable que FileStorage (erp-api-files) para sobrevivir recreates.
var dataProtectionKeysPath = Path.Combine(
    builder.Configuration["FileStorage:BasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "files"),
    "dataprotection-keys"
);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Services.AddApplication();
builder.Services.AddScoped<AppFeatureDiscoveryService>();

// OpenTelemetry metrics (Prometheus scrape en /metrics cuando Observability:EnablePrometheus=true)
var observabilityEnabled = builder.Configuration.GetValue("Observability:EnablePrometheus", true);
if (observabilityEnabled && !builder.Environment.IsEnvironment("Testing"))
{
    builder
        .Services.AddOpenTelemetry()
        .ConfigureResource(r =>
            r.AddService(
                "ERP",
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"
            )
        )
        .WithMetrics(m =>
            m.AddAspNetCoreInstrumentation().AddMeter("ERP.Security").AddPrometheusExporter()
        );
}

// Health: live = proceso arriba; ready = BD, Redis (si hay), URL externa opcional (SRI)
var healthChecks = builder
    .Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<ErpDbContextReadyHealthCheck>("database", tags: ["ready"])
    .AddCheck<SecurityContextHealthCheck>("security-context", tags: ["security"])
    .AddCheck<MembershipConsistencyHealthCheck>(
        "membership-consistency",
        tags: ["security", "ready"]
    )
    .AddCheck<MasterDataSyncHealthCheck>("masterdata-sync", tags: ["security", "ready"])
    .AddCheck<BackgroundContextHealthCheck>("background-context", tags: ["security"])
    .AddCheck<QueryFilterEnforcementHealthCheck>("query-filter-enforcement", tags: ["security"])
    .AddCheck<MasterDataReconciliationHealthCheck>(
        "masterdata-reconciliation",
        tags: ["security", "ready"]
    );

builder.Services.AddHostedService<ErpScopeMarkerStartupValidator>();

var enableRedisHealthCheck = builder.Configuration.GetValue("HealthChecks:EnableRedis", true);
if (!builder.Environment.IsEnvironment("Testing") && enableRedisHealthCheck && redisConfigured)
    healthChecks.AddRedis(redisConnection!, name: "redis", tags: ["ready"]);

var sriProbeUrl = builder.Configuration["HealthChecks:SriProbeUrl"];
if (
    !string.IsNullOrWhiteSpace(sriProbeUrl)
    && Uri.TryCreate(sriProbeUrl, UriKind.Absolute, out var sriUri)
)
{
    var probeTimeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("HealthChecks:SriProbeTimeoutSeconds", 5)
    );
    healthChecks.AddUrlGroup(
        sriUri,
        name: "sri-external",
        configureClient: (_, client) => client.Timeout = probeTimeout,
        tags: ["ready"]
    );
}

var hangfireEnabled = builder.Configuration.GetValue("Hangfire:Enabled", false);
if (hangfireEnabled)
{
    var hangfireConn =
        builder.Configuration["Hangfire:ConnectionString"]
        ?? builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(hangfireConn))
        throw new InvalidOperationException(
            "Hangfire:Enabled es true pero no hay cadena de conexión (Hangfire:ConnectionString o DefaultConnection)."
        );

    builder.Services.AddHangfire(configuration =>
        configuration.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConn))
    );
    builder.Services.AddHangfireServer();
    builder.Services.AddScoped<IProcessOutboxJob, ProcessOutboxJob>();
    builder.Services.AddScoped<IProcessCommunicationsJob, ProcessCommunicationsJob>();
    builder.Services.AddScoped<IMasterDataReconciliationJob, MasterDataReconciliationJob>();
    builder.Services.AddScoped<IElectronicDocumentRetryJob, ElectronicDocumentRetryJob>();
    builder.Services.AddScoped<IExpireUserSessionsJob, ExpireUserSessionsJob>();
}

// Opciones del Kardex: registrar tanto como IOptions<> (convención .NET)
// como plain class (inyectable directamente en Application handlers).
var kardexSection = builder.Configuration.GetSection(
    ERP.Application.Common.Config.KardexOptions.Section
);
builder.Services.Configure<ERP.Application.Common.Config.KardexOptions>(kardexSection);
builder.Services.Configure<ERP.Application.Common.Config.PasswordResetOptions>(
    builder.Configuration.GetSection(ERP.Application.Common.Config.PasswordResetOptions.SectionName)
);
builder.Services.Configure<ERP.Application.Common.Config.AuthOptions>(
    builder.Configuration.GetSection(ERP.Application.Common.Config.AuthOptions.Section)
);
builder.Services.Configure<ERP.Application.Common.Config.SriPollingOptions>(
    builder.Configuration.GetSection(ERP.Application.Common.Config.SriPollingOptions.Section)
);
builder.Services.AddSingleton(sp =>
    kardexSection.Get<ERP.Application.Common.Config.KardexOptions>()
    ?? new ERP.Application.Common.Config.KardexOptions()
);

// Política de expiración de UserSession (Fase 9) — mismo patrón que KardexOptions arriba:
// plain class inyectable directamente en ExpireUserSessionsHandler (Application).
var sessionExpirationSection = builder.Configuration.GetSection(
    ERP.Application.Common.Config.SessionExpirationOptions.Section
);
builder.Services.Configure<ERP.Application.Common.Config.SessionExpirationOptions>(
    sessionExpirationSection
);
builder.Services.AddSingleton(sp =>
    sessionExpirationSection.Get<ERP.Application.Common.Config.SessionExpirationOptions>()
    ?? new ERP.Application.Common.Config.SessionExpirationOptions()
);

// Authorization: por defecto SOLO permite tokens de sesión ERP (tenant_id válido).
builder.Services.AddSingleton<IAuthorizationHandler, HasTenantHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "Session",
        policy => policy.RequireAuthenticatedUser().AddRequirements(new HasTenantRequirement())
    );

    options.AddPolicy("IntegrationApi", policy => policy.RequireAuthenticatedUser());

    // Si el endpoint tiene [Authorize] sin policy, exigimos token de sesión.
    options.DefaultPolicy = options.GetPolicy("Session")!;
});

var app = builder.Build();

// Ensure schema is up to date before any startup queries. Migraciones + HasData() son
// prerrequisito del bootstrap global (igual que crear la Company lo es del bootstrap de
// empresa) — no un IGlobalBootstrapStep: no existe esquema utilizable antes de este punto.
using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<ErpDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}

// Comando de una sola vez (CLASS-BP-CATALOGS-01): backfill idempotente de los 12 catálogos de
// clasificación de BusinessPartner para empresas creadas antes de este bloque (p. ej. Sumak).
// No es un endpoint HTTP ni un IGlobalBootstrapStep — es una operación de despliegue explícita:
// `dotnet run -- backfill-master-data-classifications`. Sale sin iniciar el host web.
if (args.Contains("backfill-master-data-classifications"))
{
    using var backfillScope = app.Services.CreateScope();
    var backfillService =
        backfillScope.ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.MasterDataClassificationBackfillService>();
    var result = await backfillService.RunAsync();
    Console.WriteLine(
        $"[backfill-master-data-classifications] Empresas procesadas: {result.CompaniesProcessed}. Filas insertadas: {result.RowsInserted}."
    );
    return;
}

// Bootstrap global: único flujo oficial para datos de instalación (navegación + InstallData).
// Ver ERP.Infrastructure.Seeding.Global.GlobalBootstrapOrchestrator.
using (var globalBootstrapScope = app.Services.CreateScope())
{
    var globalBootstrap =
        globalBootstrapScope.ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.Global.IGlobalBootstrapOrchestrator>();
    await globalBootstrap.RunAsync();
}

// First-run: while the system is uninitialized, issue a fresh setup token and print it.
using (var firstRunScope = app.Services.CreateScope())
{
    var firstRun =
        firstRunScope.ServiceProvider.GetRequiredService<ERP.Application.Setup.IFirstRunSetupService>();
    await firstRun.EnsureSetupTokenAsync();
}

if (!app.Environment.IsProduction() && app.Configuration.GetValue("E2E:SeedEnabled", false))
{
    using var e2eSeedScope = app.Services.CreateScope();
    await e2eSeedScope
        .ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.E2E.E2ESeedService>()
        .EnsureAsync();
}

// ACCOUNTING-INITIAL-CHART-SEED-11: backfilla Plan de Cuentas/AccountingPeriod mínimos para
// companies activas creadas antes de que AccountingBootstrapStep existiera (p. ej. "ZH TECH" en
// la base local) — nunca en Production, puramente aditivo/idempotente (ver doc comment del
// servicio), sin bandera de configuración adicional porque no crea usuarios/tenants/companies.
if (!app.Environment.IsProduction())
{
    using var accountingBackfillScope = app.Services.CreateScope();
    await accountingBackfillScope
        .ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.AccountingChartBackfillService>()
        .EnsureAsync();
}

// EXPENSES-CATALOG-BOOTSTRAP-09-FIX: backfilla el catálogo de gastos (o corrige el mapeo contable
// de subcategorías ya sembradas con la cuenta incorrecta) para companies activas — nunca en
// Production. Debe correr después del backfill de Accounting porque depende de que las cuentas de
// gasto ya existan.
if (!app.Environment.IsProduction())
{
    using var expensesCatalogBackfillScope = app.Services.CreateScope();
    await expensesCatalogBackfillScope
        .ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.ExpensesCatalogBackfillService>()
        .EnsureAsync();
}

if (
    app.Environment.IsDevelopment()
    && app.Configuration.GetValue("Development:SyncFuncionalidadesOnStartup", false)
)
{
    using var syncScope = app.Services.CreateScope();
    await syncScope
        .ServiceProvider.GetRequiredService<AppFeatureDiscoveryService>()
        .SyncFeaturesAsync();
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRequestCorrelation();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("Frontend");

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("live"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }
);

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }
);

app.MapHealthChecks(
    "/health/security-context",
    new HealthCheckOptions
    {
        Predicate = r => r.Name == "security-context",
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }
);
app.MapHealthChecks(
    "/health/membership-consistency",
    new HealthCheckOptions
    {
        Predicate = r => r.Name == "membership-consistency",
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }
);
app.MapHealthChecks(
    "/health/masterdata-sync",
    new HealthCheckOptions
    {
        Predicate = r => r.Name == "masterdata-sync",
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }
);
app.MapHealthChecks(
    "/health/background-context",
    new HealthCheckOptions
    {
        Predicate = r => r.Name == "background-context",
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }
);
app.MapHealthChecks(
    "/health/query-filter-enforcement",
    new HealthCheckOptions
    {
        Predicate = r => r.Name == "query-filter-enforcement",
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }
);
app.MapHealthChecks(
    "/health/masterdata-reconciliation",
    new HealthCheckOptions
    {
        Predicate = r => r.Name == "masterdata-reconciliation",
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }
);
if (observabilityEnabled && !app.Environment.IsEnvironment("Testing"))
    app.MapPrometheusScrapingEndpoint("/metrics");

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseSecurityCorrelation();
app.UseMiddleware<EnterpriseDiagnosticMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ForbiddenAccessLoggingMiddleware>();

if (hangfireEnabled)
{
    var dashEnabled = app.Configuration.GetValue("Hangfire:Dashboard:Enabled", true);
    if (dashEnabled)
    {
        var dashPath = app.Configuration["Hangfire:Dashboard:Path"] ?? "/hangfire";
        app.UseHangfireDashboard(
            dashPath,
            new DashboardOptions { Authorization = [new HangfireDashboardAuthorizationFilter()] }
        );
    }

    RecurringJob.AddOrUpdate<IProcessOutboxJob>(
        "process-outbox",
        x => x.ExecuteAsync(CancellationToken.None),
        "* * * * *"
    );

    RecurringJob.AddOrUpdate<IProcessCommunicationsJob>(
        "process-communications",
        x => x.ExecuteAsync(CancellationToken.None),
        "* * * * *"
    );

    RecurringJob.AddOrUpdate<IMasterDataReconciliationJob>(
        "masterdata-reconciliation",
        x => x.ExecuteAsync(CancellationToken.None),
        Cron.Daily(hour: 3)
    );

    RecurringJob.AddOrUpdate<IElectronicDocumentRetryJob>(
        "electronic-document-retry",
        x => x.ExecuteAsync(CancellationToken.None),
        "* * * * *"
    );

    RecurringJob.AddOrUpdate<IExpireUserSessionsJob>(
        "expire-user-sessions",
        x => x.ExecuteAsync(CancellationToken.None),
        Cron.Daily(hour: 4)
    );
}

app.MapControllers();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
