using Microsoft.Extensions.DependencyInjection;
using ERP.Domain.Products.Interfaces;
using ERP.Infrastructure.Repositories;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            // Registrar DbContext
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            // Registrar Repositorios
            services.AddScoped<IProductRepository, ProductRepository>();
            // Agrega aquí más repositorios en el futuro (Accounting, Tenants, etc.)
            return services;
        }
    }
}