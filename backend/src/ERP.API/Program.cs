using ERP.Infrastructure; // <--- ESTO ES VITAL
using Microsoft.AspNetCore.Builder; // Asegura que este presente
using Microsoft.EntityFrameworkCore; // <--- Agrega esto
var builder = WebApplication.CreateBuilder(args);

// 1. REGISTRAR SERVICIOS (TODO lo que sea builder.Services...)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration); // Tu inyección personalizada
builder.Services.AddCors(options => 
{
    options.AddPolicy("AllowReactApp", policy => 
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 2. CONSTRUIR LA APLICACIÓN
var app = builder.Build(); 

// 3. MIDDLEWARE (Configurar el pipeline - AQUÍ SÍ PUEDES USAR 'app')
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.MapControllers();

// Tu endpoint de prueba
app.MapGet("/test-db", async (ERP.Infrastructure.Persistence.AppDbContext db) =>
{
    var count = await db.Products.CountAsync();
    return Results.Ok(new { Message = "Conexión exitosa", ProductCount = count });
});

app.Run();