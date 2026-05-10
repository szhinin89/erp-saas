using ERP.API.Extensions;
using ERP.API.Middleware;
using ERP.Infrastructure;
using ERP.Application;
using ERP.API.Authorization;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Services;
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
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Opciones del Kardex: registrar tanto como IOptions<> (convención .NET)
// como plain class (inyectable directamente en Application handlers).
var kardexSection = builder.Configuration.GetSection(
    ERP.Application.Common.Config.KardexOptions.Section);
builder.Services.Configure<ERP.Application.Common.Config.KardexOptions>(kardexSection);
builder.Services.AddSingleton(sp =>
    kardexSection.Get<ERP.Application.Common.Config.KardexOptions>()
    ?? new ERP.Application.Common.Config.KardexOptions());

// Registro condicional del servicio SRI
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<ISriFacturaElectronicaService, SriFacturaElectronicaSimuladoService>();
else
    builder.Services.AddScoped<ISriFacturaElectronicaService, SriFacturaElectronicaRealService>();

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
app.MapControllers();

app.Run();

public partial class Program { }
