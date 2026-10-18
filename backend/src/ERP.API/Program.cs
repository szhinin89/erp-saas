// CA1848/CA1873: Program.cs top-level statements cannot use [LoggerMessage] source generators
#pragma warning disable CA1848, CA1873

using System.Security.Claims;
using System.Threading.RateLimiting;
using ERP.API.Authorization;
using ERP.API.Extensions;
using ERP.API.Hangfire;
using ERP.API.Hosting;
using ERP.API.Health;
using ERP.API.Middleware;
using ERP.API.Services;
using ERP.Application;
using ERP.Domain.Kernel.Security;
using ERP.Infrastructure;
using ERP.Infrastructure.Caching;
using ERP.Infrastructure.Persistence;
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

// Licencia Community: libre para proyectos con ingresos anuales < 1 M USD.
// Cambiar a LicenseType.Professional si aplica.
QuestPDF.Settings.License = LicenseType.Community;

if (args.Contains("--worker", StringComparer.Ordinal))
{
    using var worker = BackgroundWorkerHost.Build(args.Where(a => a != "--worker").ToArray());
    await worker.RunAsync();
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Fuentes de configuración comunes con el worker (precedencia y alias DB_CONNECTION_STRING).
builder.Configuration.AddErpConfigurationSources(builder.Environment, args);

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

builder
    .Services.AddControllers(opts =>
    {
        // ZH-TEMPORAL-CONTRACT-02: DateTime en query/route = instante con zona explícita.
        opts.ModelBinderProviders.Insert(0, new ERP.API.Temporal.UtcInstantModelBinderProvider());
    })
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
        // ZH-TEMPORAL-CONTRACT-02: DateTime en JSON = instante UTC ("...Z"); DateOnly = "YYYY-MM-DD".
        opts.JsonSerializerOptions.Converters.Add(new ERP.API.Temporal.UtcInstantJsonConverter());
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
    // ZH-API-ERROR-CONTRACT-HARDENING-01: el rechazo del limitador usa el mismo contrato que
    // cualquier otro error (envelope RATE_LIMITED, status de ApiErrorStatus) — antes era un 429
    // sin cuerpo, distinto del RATE_LIMITED que devuelve /auth/refresh vía Result.
    options.OnRejected = (context, cancellationToken) =>
    {
        var http = context.HttpContext;
        var code = ERP.Application.Common.ApiResponseCodes.Common.RateLimited;
        http.Response.StatusCode = ApiErrorStatus.For(code);
        return new ValueTask(
            http.Response.WriteAsJsonAsync(
                ResponseFactory.Error(
                    http,
                    http.RequestServices.GetRequiredService<IWebHostEnvironment>(),
                    code
                ),
                http.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions,
                cancellationToken
            )
        );
    };
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

    // ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — solo forgot-password (reset-password con token
    // legítimo no comparte esta política). Límites en PasswordReset:IpRequestLimit/IpRequestWindowMinutes.
    var passwordResetDefaults = new ERP.Application.Common.Config.PasswordResetOptions();
    var forgotPasswordIpLimit = Math.Max(
        1,
        builder.Configuration.GetValue(
            $"{ERP.Application.Common.Config.PasswordResetOptions.SectionName}:IpRequestLimit",
            passwordResetDefaults.IpRequestLimit
        )
    );
    var forgotPasswordIpWindowMinutes = Math.Max(
        1,
        builder.Configuration.GetValue(
            $"{ERP.Application.Common.Config.PasswordResetOptions.SectionName}:IpRequestWindowMinutes",
            passwordResetDefaults.IpRequestWindowMinutes
        )
    );
    options.AddPolicy(
        "auth-forgot-password-ip",
        httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                ip,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = forgotPasswordIpLimit,
                    Window = TimeSpan.FromMinutes(forgotPasswordIpWindowMinutes),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }
            );
        }
    );
});

// Registro común API/worker: logging, caché distribuida, Infrastructure/Application, Data
// Protection y opciones (ErpHostRegistration).
builder.Services.AddErpHostCore(builder.Configuration, builder.Environment);
var redisConnection = ErpHostRegistration.RedisConnection(builder.Configuration);
builder.Services.AddScoped<AppFeatureDiscoveryService>();
builder.Services.AddScoped<ERP.API.Diagnostics.CacheHealthProbe>(); // DEV ONLY: /api/dev/redis-health

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
            m.AddAspNetCoreInstrumentation().AddMeter("ERP.Security", "ERP.Jobs").AddPrometheusExporter()
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
        tags: ["security"]
    )
    .AddCheck<MasterDataSyncHealthCheck>("masterdata-sync", tags: ["security"])
    .AddCheck<BackgroundContextHealthCheck>("background-context", tags: ["security"])
    .AddCheck<QueryFilterEnforcementHealthCheck>("query-filter-enforcement", tags: ["security"]);

builder.Services.AddHostedService<ErpScopeMarkerStartupValidator>();

var enableRedisHealthCheck = builder.Configuration.GetValue("HealthChecks:EnableRedis", true);
if (!builder.Environment.IsEnvironment("Testing") && enableRedisHealthCheck && redisConnection is not null)
    healthChecks.AddRedis(redisConnection, name: "redis", tags: ["ready"]);

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

builder.Services.AddErpBackgroundProcessing(builder.Configuration);

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

    options.AddPolicy(
        "IntegrationApi",
        policy => policy.RequireAuthenticatedUser().RequireClaim("integration_api", "true")
    );

    options.AddPolicy(
        "CompanyProvisioning",
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                {
                    var hasGlobalTenant = context.User.HasClaim("tenant_id", Guid.Empty.ToString());

                    var hasAdminRole =
                        context.User.IsInRole(SecurityRoles.Admin)
                        || context.User.Claims.Any(c =>
                            (c.Type == ClaimTypes.Role || c.Type == "role")
                            && c.Value == SecurityRoles.Admin
                        );

                    return hasGlobalTenant && hasAdminRole;
                })
    );

    options.AddPolicy(
        "PlatformAdmin",
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                {
                    var hasGlobalTenant = context.User.HasClaim("tenant_id", Guid.Empty.ToString());

                    var hasAdminRole =
                        context.User.IsInRole(SecurityRoles.Admin)
                        || context.User.Claims.Any(c =>
                            (c.Type == ClaimTypes.Role || c.Type == "role")
                            && c.Value == SecurityRoles.Admin
                        );

                    return hasGlobalTenant && hasAdminRole;
                })
    );

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

// Comando de una sola vez (BANK-CATALOG-01): backfill idempotente del catálogo mínimo de Bancos
// para tenants creados antes de este ticket — BankCatalogBootstrapStep solo siembra empresas
// NUEVAS. No es un endpoint HTTP ni un IGlobalBootstrapStep — operación de despliegue explícita:
// `dotnet run -- backfill-bank-catalog`. Sale sin iniciar el host web.
if (args.Contains("backfill-bank-catalog"))
{
    using var bankCatalogBackfillScope = app.Services.CreateScope();
    var bankCatalogBackfillService =
        bankCatalogBackfillScope.ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.BankCatalogBackfillService>();
    var bankCatalogResult = await bankCatalogBackfillService.RunAsync();
    Console.WriteLine(
        $"[backfill-bank-catalog] Tenants procesados: {bankCatalogResult.TenantsProcessed}. Bancos insertados: {bankCatalogResult.RowsInserted}."
    );
    return;
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

// Comando de una sola vez (SALES-PAYMENT-METHOD-SRI-MAPPING-EFECTIVO-WRONG-CODE-01): backfill
// idempotente de PaymentMethod.SriPaymentMethodCode para tenants creados antes de
// SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01 — SalesBootstrapStep solo siembra formas de cobro para
// empresas NUEVAS. Nunca pisa un mapeo ya configurado manualmente; solo aplica el código sugerido
// si existe y está activo en el catálogo real global.sri_payment_method. No es un endpoint HTTP ni
// un IGlobalBootstrapStep — operación de despliegue explícita:
// `dotnet run -- backfill-payment-method-sri-mapping`. Sale sin iniciar el host web.
if (args.Contains("backfill-payment-method-sri-mapping"))
{
    using var paymentMethodBackfillScope = app.Services.CreateScope();
    var paymentMethodBackfillService =
        paymentMethodBackfillScope.ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.PaymentMethodSriMappingBackfillService>();
    var paymentMethodResult = await paymentMethodBackfillService.RunAsync();
    Console.WriteLine(
        $"[backfill-payment-method-sri-mapping] Filas sin mapeo encontradas: {paymentMethodResult.CandidatesFound}. "
            + $"Actualizadas: {paymentMethodResult.RowsUpdated}. "
            + $"Sin código sugerido (p. ej. Crédito): {paymentMethodResult.SkippedNoMapping}. "
            + $"Con código sugerido inactivo en catálogo: {paymentMethodResult.SkippedInactiveCatalog}."
    );
    return;
}

// Comando de una sola vez (SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01): backfill idempotente de
// CashRegister.AccountingAccountId (-> Caja general) para companies creadas antes de este ticket
// — sin esto, toda venta al contado de una company existente con una caja sin cuenta configurada
// empezaría a fallar fail-closed (AuthorizeSalesInvoiceHandler ya no cae en PaymentMethodAccount
// para Efectivo). No es un endpoint HTTP ni un IGlobalBootstrapStep — operación de despliegue
// explícita: `dotnet run -- backfill-cash-register-accounting-account`. Sale sin iniciar el host web.
if (args.Contains("backfill-cash-register-accounting-account"))
{
    using var cashRegisterAccountBackfillScope = app.Services.CreateScope();
    var cashRegisterAccountBackfillService =
        cashRegisterAccountBackfillScope.ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.CashRegisterAccountingAccountBackfillService>();
    var cashRegisterAccountResult = await cashRegisterAccountBackfillService.RunAsync();
    Console.WriteLine(
        $"[backfill-cash-register-accounting-account] Companies con cajas pendientes: {cashRegisterAccountResult.CompaniesProcessed}. "
            + $"Cajas actualizadas: {cashRegisterAccountResult.RowsUpdated}. "
            + $"Sin cuenta Caja general activa/postable: {cashRegisterAccountResult.SkippedNoCajaGeneralAccount}."
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

// EXPENSES-CATALOG-BOOTSTRAP-09-FIX: backfilla el catálogo de gastos (o corrige el mapeo contable
// de subcategorías ya sembradas con la cuenta incorrecta) para companies activas — nunca en
// Production. Depende de que las cuentas de gasto ya existan (AccountingBootstrapStep).
if (!app.Environment.IsProduction())
{
    using var expensesCatalogBackfillScope = app.Services.CreateScope();
    await expensesCatalogBackfillScope
        .ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.ExpensesCatalogBackfillService>()
        .EnsureAsync();
}

// DOCUMENT-FLOW-POLICY-01: backfilla DocumentFlowPolicy (una fila por DocType activo) para
// companies activas creadas antes de que DocumentFlowPolicyBootstrapStep existiera — nunca en
// Production, puramente aditivo/idempotente (ver doc comment del servicio).
if (!app.Environment.IsProduction())
{
    using var documentFlowPolicyBackfillScope = app.Services.CreateScope();
    await documentFlowPolicyBackfillScope
        .ServiceProvider.GetRequiredService<ERP.Infrastructure.Seeding.DocumentFlowPolicyBackfillService>()
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
