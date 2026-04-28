using ERP.Application;
using ERP.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// Services Registration (Clean Architecture)
// =============================================

builder.Services.AddApplication();           // Capa de Aplicación
builder.Services.AddInfrastructure(builder.Configuration);  // Capa de Infraestructura

// API Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// =============================================
// Middleware Pipeline
// =============================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();