using ERP.API.Extensions;
using ERP.API.Middleware;
using ERP.Infrastructure;
using ERP.Application;
using ERP.API.Authorization;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

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

// Authorization: por defecto SOLO permite tokens de sesión (no bootstrap).
builder.Services.AddSingleton<IAuthorizationHandler, TokenTypeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Session", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TokenTypeRequirement("session")));

    options.AddPolicy("Bootstrap", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TokenTypeRequirement("bootstrap")));

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
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
