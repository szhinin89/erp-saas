using ERP.Domain.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore; // Asegúrate de tener este using
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. REGISTRA TU DB CONTEXT AQUÍ
        // Asegúrate de que el nombre "DefaultConnection" coincida con tu appsettings.json
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // 2. Registra tus repositorios
        services.AddScoped<IProductRepository, ProductRepository>();
        
        return services;
    }
}