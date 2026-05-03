using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Common;
using ERP.Domain.Accounting.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using ERP.Domain.Security.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Geography.Interfaces;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Customers.Interfaces;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Application.Navigation;
using ERP.Application.Subscriptions;
using ERP.Infrastructure.Deployment;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Services;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<InstanceQuotaFileStore>();
        services.AddSingleton<IDeploymentFeatureFlags, DeploymentFeatureFlags>();

        services.AddDbContext<ErpDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICurrentTenant, CurrentTenantService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<IAccountingRepository, AccountingRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ITaxRateRepository, TaxRateRepository>();
        services.AddScoped<IProductCatalogRepository, ProductCatalogRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccessRepository, AccessRepository>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ISecurityRepository, SecurityRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IGeographyReadRepository, GeographyReadRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<SaasCatalogQuery>();
        services.AddScoped<ISaasCatalogQuery>(sp => sp.GetRequiredService<SaasCatalogQuery>());
        services.AddScoped<ISaasPublicPlansQuery>(sp => sp.GetRequiredService<SaasCatalogQuery>());
        services.AddScoped<ISaasPlansAdminService, SaasPlansAdminService>();
        services.AddScoped<INavigationMenuReader, NavigationMenuReader>();
        services.AddScoped<INavigationMenuAdminService, NavigationMenuAdminService>();

        return services;
    }
}

