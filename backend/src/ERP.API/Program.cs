using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using ERP.API.Hangfire;
using ERP.API.Health;
using ERP.API.Extensions;
using ERP.API.Middleware;
using ERP.API.Services;
using ERP.Infrastructure;
using ERP.Application;
using ERP.API.Authorization;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.InstallData;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuestPDF.Infrastructure;
using System.Threading.RateLimiting;
using Serilog;

// Licencia Community: libre para proyectos con ingresos anuales < 1 M USD.
// Cambiar a LicenseType.Professional si aplica.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Asegura que los user-secrets del API ganen a appsettings (p. ej. InitialSuperAdminSetupToken vacío en JSON).
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

// Alias opcional en hosting: DB_CONNECTION_STRING → ConnectionStrings:DefaultConnection
var dbFromEnv = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(dbFromEnv))
{
    builder.Configuration.AddInMemoryCollection([
        new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", dbFromEnv)
    ]);
}

builder.Host.UseSerilog((context, _, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ERP.SaaS");
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:5173"];

        policy.WithOrigins(origins)
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
              .WithHeaders("Authorization", "Content-Type", "Accept")
              .AllowCredentials();
    });
});

builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-subscriber", httpContext =>
    {
        var subscriberId = httpContext.User.FindFirst("subscriber_id")?.Value ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            subscriberId,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });
});

// IDistributedCache: Redis si Redis:ConnectionString o ConnectionStrings:Redis está definida;
// en tests y producción sin Redis, memoria (no comparte entre instancias).
var redisConnection = builder.Configuration["Redis:ConnectionString"]
                      ?? builder.Configuration.GetConnectionString("Redis");
var redisInstanceName = builder.Configuration["Redis:InstanceName"];
if (string.IsNullOrWhiteSpace(redisInstanceName))
    redisInstanceName = "ERP_";

if (!string.IsNullOrWhiteSpace(redisConnection))
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

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDataProtection();
builder.Services.AddApplication();
builder.Services.AddScoped<AppFeatureDiscoveryService>();

// Health: live = proceso arriba; ready = BD, Redis (si hay), URL externa opcional (SRI)
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<ErpDbContextReadyHealthCheck>("database", tags: ["ready"]);

var enableRedisHealthCheck = builder.Configuration.GetValue("HealthChecks:EnableRedis", true);
if (!builder.Environment.IsEnvironment("Testing")
    && enableRedisHealthCheck
    && !string.IsNullOrWhiteSpace(redisConnection))
    healthChecks.AddRedis(redisConnection, name: "redis", tags: ["ready"]);

var sriProbeUrl = builder.Configuration["HealthChecks:SriProbeUrl"];
if (!string.IsNullOrWhiteSpace(sriProbeUrl)
    && Uri.TryCreate(sriProbeUrl, UriKind.Absolute, out var sriUri))
{
    var probeTimeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("HealthChecks:SriProbeTimeoutSeconds", 5));
    healthChecks.AddUrlGroup(
        sriUri,
        name: "sri-external",
        configureClient: (_, client) => client.Timeout = probeTimeout,
        tags: ["ready"]);
}

var hangfireEnabled = builder.Configuration.GetValue("Hangfire:Enabled", false);
if (hangfireEnabled)
{
    var hangfireConn = builder.Configuration["Hangfire:ConnectionString"]
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(hangfireConn))
        throw new InvalidOperationException(
            "Hangfire:Enabled es true pero no hay cadena de conexión (Hangfire:ConnectionString o DefaultConnection).");

    builder.Services.AddHangfire(configuration =>
        configuration.UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(hangfireConn)));
    builder.Services.AddHangfireServer();
    builder.Services.AddScoped<ISriRetryJob, SriRetryJob>();
}

// Opciones del Kardex: registrar tanto como IOptions<> (convención .NET)
// como plain class (inyectable directamente en Application handlers).
var kardexSection = builder.Configuration.GetSection(
    ERP.Application.Common.Config.KardexOptions.Section);
builder.Services.Configure<ERP.Application.Common.Config.KardexOptions>(kardexSection);
builder.Services.Configure<ERP.Application.Common.Config.PasswordResetOptions>(
    builder.Configuration.GetSection(ERP.Application.Common.Config.PasswordResetOptions.SectionName));
builder.Services.Configure<ERP.Application.Common.Config.SaasEntitlementsOptions>(
    builder.Configuration.GetSection(ERP.Application.Common.Config.SaasEntitlementsOptions.Section));
builder.Services.AddSingleton(sp =>
    kardexSection.Get<ERP.Application.Common.Config.KardexOptions>()
    ?? new ERP.Application.Common.Config.KardexOptions());

// Authorization: por defecto SOLO permite tokens de sesión (no bootstrap).
builder.Services.AddSingleton<IAuthorizationHandler, TokenTypeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, GlobalSuperAdminHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Session", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TokenTypeRequirement("session")));

    options.AddPolicy("Bootstrap", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TokenTypeRequirement("bootstrap")));

    options.AddPolicy("GlobalSuperAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TokenTypeRequirement("session"))
              .AddRequirements(new GlobalSuperAdminRequirement()));

    // Si el endpoint tiene [Authorize] sin policy, exigimos token de sesión.
    // IMPORTANTE: NO usar FallbackPolicy, porque eso protegería endpoints que
    // deben ser públicos (login/bootstrap-login/reset, etc.).
    // Criterio perm vs Session vs Roles: docs/STATUS.md (backlog IAM / refactor P0).
    options.DefaultPolicy = options.GetPolicy("Session")!;
});

var app = builder.Build();

// Ensure schema is up to date before any startup queries
// (e.g., CommercialPlansBootstrap reading commercial_plans).
using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<ErpDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue("Development:SyncFuncionalidadesOnStartup", false))
{
    using var syncScope = app.Services.CreateScope();
    await syncScope.ServiceProvider.GetRequiredService<AppFeatureDiscoveryService>().SyncFeaturesAsync();
}

// Catálogo mínimo de planes SaaS en BD (idempotente) — antes del seed demo (entitlements).
if (!app.Environment.IsEnvironment("Testing")
    || !app.Configuration.GetValue("Testing:SkipCommercialPlansBootstrap", false))
{
    using var plansScope = app.Services.CreateScope();
    var db = plansScope.ServiceProvider.GetRequiredService<ErpDbContext>();
    await CommercialPlansBootstrap.EnsureDefaultsAsync(db);
    await CommercialPlanFeaturesBootstrap.EnsureDefaultsAsync(db);
    await CommercialPlanLimitsBootstrap.EnsureDefaultsAsync(db);
}

// Datos demo (subscriber-demo + admin) solo si se activa explícitamente — ver appsettings.Development → Development:SeedDemoSubscriber.
if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue("Development:SeedDemoTenant", false))
{
    await DevDatabaseSeeder.SeedMinimumAsync(app.Services);
}

// InstallData: carga automática de datos base (idempotente por script/checksum).
using (var installDataScope = app.Services.CreateScope())
{
    try
    {
        var installData = installDataScope.ServiceProvider.GetRequiredService<IInstallDataBootstrapService>();
        await installData.ApplyPendingAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "InstallData failed at startup. Continuing without blocking API startup.");
    }
}

// Bootstrap seguro de primera ejecución (omitido en Testing).
if (!app.Environment.IsEnvironment("Testing")
    || !app.Configuration.GetValue("Testing:SkipFirstRunSetup", false))
{
    using var setupScope = app.Services.CreateScope();
    var firstRunSetup = setupScope.ServiceProvider.GetRequiredService<IFirstRunSetupService>();
    var setupResult = await firstRunSetup.EnsureTokenIssuedAsync();
    if (setupResult.IsFirstRun && setupResult.TokenGenerated && !string.IsNullOrWhiteSpace(setupResult.PlainToken))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("==================================================");
        Console.WriteLine("FIRST-RUN DETECTADO: crear SUPER ADMIN inicial");
        Console.WriteLine("Ejecuta desde la máquina del servidor (mismo body en /api/setup/claim-initial-superadmin):");
        Console.WriteLine(
            "curl -X POST https://localhost:5001/api/setup/superadmin " +
            "-H \"Content-Type: application/json\" " +
            "-d '{\"setupToken\":\"" + setupResult.PlainToken + "\",\"firstName\":\"Super\",\"lastName\":\"Admin\",\"email\":\"superadmin@erp.com\",\"password\":\"CAMBIAR-ESTA-CLAVE\"}'");
        Console.WriteLine("Documentación: docs/DEVELOPMENT.md | script: .\\Crear-SuperAdmin.ps1");
        Console.WriteLine("Token expira en: " + setupResult.ExpiresAtUtc?.ToString("u"));
        Console.WriteLine("==================================================");
        Console.ResetColor();
    }
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("Frontend");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
});

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<EnterpriseDiagnosticMiddleware>();
app.UseMiddleware<SuperAdminPanelLockMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ForbiddenAccessLoggingMiddleware>();

if (hangfireEnabled)
{
    var dashEnabled = app.Configuration.GetValue("Hangfire:Dashboard:Enabled", true);
    if (dashEnabled)
    {
        var dashPath = app.Configuration["Hangfire:Dashboard:Path"] ?? "/hangfire";
        app.UseHangfireDashboard(dashPath, new DashboardOptions
        {
            Authorization = [new HangfireDashboardAuthorizationFilter()],
        });
    }

    RecurringJob.AddOrUpdate<IKardexDatabaseMaintenance>(
        "refresh-mv-saldos-diarios",
        x => x.RefreshDailyBalancesMaterializedViewAsync(CancellationToken.None),
        Cron.Daily(hour: 1));

    RecurringJob.AddOrUpdate<ISriRetryJob>(
        "sri-emission-retry",
        x => x.ExecuteAsync(CancellationToken.None),
        "*/5 * * * *"); // cada 5 minutos
}

app.UseSerilogRequestLogging();

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
