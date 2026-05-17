using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Logistics.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using ERP.Domain.Security.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Geography.Interfaces;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Cash.Interfaces;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Application.Navigation;
using ERP.Application.Subscriptions;
using ERP.Application.Admin;
using ERP.Infrastructure.BackgroundServices;
using ERP.Infrastructure.Deployment;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Saas;
using ERP.Infrastructure.Security;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Services.Cash;
using ERP.Infrastructure.Seeding;
using ERP.Infrastructure.Seeding.InstallData;
using ERP.Domain.Modules.Cash;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<InstanceQuotaFileStore>();
        services.AddSingleton<IDeploymentFeatureFlags, DeploymentFeatureFlags>();
        services.AddScoped<IFirstRunSetupService, FirstRunSetupService>();
        services.Configure<InstallDataOptions>(configuration.GetSection(InstallDataOptions.SectionName));
        services.AddScoped<IInstallDataBootstrapService, InstallDataBootstrapService>();

        services.AddDbContext<ErpDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)));

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IPasswordResetLinkSender, LoggingPasswordResetLinkSender>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICurrentTenant, CurrentTenantService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<IAccountingRepository, AccountingRepository>();
        services.AddScoped<IAccountingSetupRepository, ConfiguracionContableRepository>();
        services.AddScoped<ICuentaContableService, CuentaContableService>();
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
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IXmlFacturaParser, SriFacturaParser>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<ISriFacturaElectronicaService, SriFacturaElectronicaSimuladoService>();
        services.AddScoped<ISriComprobanteRetentionService, SriWithholdingSimulatedService>();
        services.AddScoped<IPurchBillRepository, PurchBillRepository>();
        services.AddScoped<IExpenseInvoiceRepository, ExpenseInvoiceRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IKardexSnapshotRepository, KardexSnapshotRepository>();
        services.AddScoped<IKardexReportRepository, KardexReportRepository>();
        services.AddScoped<IKardexDatabaseMaintenance, KardexDatabaseMaintenanceService>();
        services.AddScoped<IKardexMaterializedDailySummariesReader, KardexMaterializedDailySummariesReader>();
        services.AddScoped<ICostoPromedioService, CostoPromedioService>();
        services.AddScoped<KardexSnapshotService>();
        services.AddScoped<IKardexSnapshotCalculator>(sp => sp.GetRequiredService<KardexSnapshotService>());
        services.AddSingleton<KardexReportQueue>();
        services.AddHostedService<KardexSnapshotWorker>();
        services.AddHostedService<KardexReportProcessor>();
        services.AddScoped<IStockTransferRepository, StockTransferRepository>();
        services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<SaasCatalogQuery>();
        services.AddScoped<ISaasCatalogQuery>(sp => sp.GetRequiredService<SaasCatalogQuery>());
        services.AddScoped<ISaasPublicPlansQuery>(sp => sp.GetRequiredService<SaasCatalogQuery>());
        services.AddScoped<ISaasPlansAdminService, SaasPlansAdminService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddScoped<INavigationMenuReader, NavigationMenuReader>();
        services.AddScoped<TenantMenuService>();
        services.AddScoped<ITenantSessionMenuResolver>(sp => sp.GetRequiredService<TenantMenuService>());
        services.AddScoped<ITenantMenuAdminService>(sp => sp.GetRequiredService<TenantMenuService>());
        services.AddScoped<INavigationMenuAdminService, NavigationMenuAdminService>();
        services.AddScoped<IGrowthAnalyticsReader, GrowthAnalyticsReader>();
        services.AddScoped<IBillingSettingsRepository, BillingSettingsRepository>();
        services.AddScoped<ISriSettingsRepository, SriSettingsRepository>();
        services.AddScoped<IRetentionSettingsRepository, RetentionSettingsRepository>();
        services.AddScoped<ISalesRepository, SalesRepository>();
        services.AddScoped<ITirillaFacturaService, TirillaFacturaService>();
        services.AddScoped<ICashRepository, CajaRepository>();
        services.AddScoped<IStatementParser, BankStatementCsvParser>();
        services.AddScoped<ICarrierRepository, CarrierRepository>();
        services.AddScoped<IDefaultProfileSeeder, DefaultProfileSeeder>();
        services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();

        return services;
    }
}

