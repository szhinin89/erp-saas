using ERP.API.Extensions;
using ERP.API.Middleware;
using ERP.Infrastructure;
using ERP.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// Handlers
builder.Services.AddScoped<ERP.Application.Auth.UseCases.Register.RegisterHandler>();
builder.Services.AddScoped<ERP.Application.Auth.UseCases.Login.LoginHandler>();
builder.Services.AddScoped<ERP.Application.Accounting.UseCases.CreateAccount.CreateAccountHandler>();
builder.Services.AddScoped<ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry.CreateJournalEntryHandler>();
builder.Services.AddScoped<ERP.Application.Products.UseCases.CreateProduct.CreateProductHandler>();
builder.Services.AddScoped<ERP.Application.Tenants.UseCases.CreateTenant.CreateTenantHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
