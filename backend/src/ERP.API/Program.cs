using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using ERP.API.Hangfire;
using ERP.API.Extensions;
using ERP.API.Middleware;
using ERP.Infrastructure;
using ERP.Application;
using ERP.API.Authorization;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Infrastructure;

// Licencia Community: libre para proyectos con ingresos anuales < 1 M USD.
// Cambiar a LicenseType.Professional si aplica.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Asegura que los user-secrets del API ganen a appsettings (p. ej. InitialSuperAdminSetupToken vacío en JSON).
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

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
builder.Services.AddApplication();

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
}

// Opciones del Kardex: registrar tanto como IOptions<> (convención .NET)
// como plain class (inyectable directamente en Application handlers).
var kardexSection = builder.Configuration.GetSection(
    ERP.Application.Common.Config.KardexOptions.Section);
builder.Services.Configure<ERP.Application.Common.Config.KardexOptions>(kardexSection);
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
    // Criterio perm vs Session vs Roles: docs/REFACTOR-BACKLOG.md sección P0.
    options.DefaultPolicy = options.GetPolicy("Session")!;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("Frontend");

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<SuperAdminPanelLockMiddleware>();
app.UseAuthorization();

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
}

app.MapControllers();

app.Run();

public partial class Program { }
