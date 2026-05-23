using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Logistics.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
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
using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.MasterData;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.MasterData;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Application.Subscriptions;
using ERP.Application.Subscriptions.Caching;
using ERP.Infrastructure.Access.Caching;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Application.Navigation;
using ERP.Application.Admin;
using ERP.Infrastructure.BackgroundServices;
using ERP.Infrastructure.Deployment;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Saas;
using ERP.Infrastructure.Security;
using ERP.Application.Billing.Governance;
using ERP.Application.Billing.PaymentProviders;
using ERP.Application.Common;
using ERP.Application.Subscriptions.Caching;
using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Billing.Interfaces;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Services.CommercialLimitUsage;
using ERP.Infrastructure.Subscriptions.Caching;
using ERP.Infrastructure.Services.Cash;
using ERP.Infrastructure.Seeding;
using ERP.Infrastructure.Seeding.InstallData;
using ERP.Domain.Modules.Cash;
using ERP.Domain.Modules.Menu.Interfaces;
using ERP.Infrastructure.Options;
using ERP.Infrastructure.Persistence.Outbox;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Security;
using ERP.Infrastructure.Observability;

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
        services.AddSingleton<IInstanceQuotaPersistence, InstanceQuotaPersistence>();
        services.AddSingleton<IDeploymentFeatureFlags, DeploymentFeatureFlags>();
        services.AddScoped<IFirstRunSetupService, FirstRunSetupService>();
        services.Configure<InstallDataOptions>(configuration.GetSection(InstallDataOptions.SectionName));
        services.Configure<DocumentSchemaOptions>(configuration.GetSection(DocumentSchemaOptions.SectionName));
        services.AddScoped<IInstallDataBootstrapService, InstallDataBootstrapService>();

        services.AddScoped<PostgreSqlSessionContextInterceptor>();
        services.AddDbContext<ErpDbContext>((sp, options) =>
            options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName))
                .AddInterceptors(sp.GetRequiredService<PostgreSqlSessionContextInterceptor>()));

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<RefreshTokenRateLimiter>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IPasswordResetLinkSender, LoggingPasswordResetLinkSender>();
        services.AddScoped<IUnifiedDocumentSync, UnifiedDocumentSync>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlatformQueryAccessor, PlatformQueryAccessor>();
        services.AddScoped<ISubscriptionFeatureOverridesService, SubscriptionFeatureOverridesService>();
        services.AddScoped<ICurrentSubscriber, CurrentSubscriberService>();
        services.AddScoped<ICurrentCompany, CurrentCompanyService>();
        services.AddScoped<ISessionContext, HttpSessionContext>();
        services.AddScoped<IDbSessionContextApplicator, DbSessionContextApplicator>();
        services.AddScoped<IOperationalContext, OperationalContextService>();
        services.AddScoped<IMembershipAuthority, MembershipAuthority>();

        // ── MasterData BC ────────────────────────────────────────────────
        services.AddScoped<IBusinessPartnerRepository, BusinessPartnerRepository>();
        services.AddScoped<IBusinessPartnerOperationalLinkEnricher, BusinessPartnerOperationalLinkEnricher>();
        services.AddScoped<ICustomerProfileRepository, CustomerProfileRepository>();
        services.AddScoped<ISupplierProfileRepository, SupplierProfileRepository>();
        services.AddScoped<ICompanyBpSettingsRepository, CompanyBpSettingsRepository>();
        services.AddScoped<DistributedPermissionsCacheService>();
        services.AddScoped<ResilientPermissionsCacheService>();
        services.AddScoped<IPermissionsCacheBackend>(sp => sp.GetRequiredService<ResilientPermissionsCacheService>());
        services.AddScoped<IPermissionsCacheService>(sp => sp.GetRequiredService<ResilientPermissionsCacheService>());
        services.AddScoped<IPermissionsCacheInvalidator, PermissionsCacheInvalidator>();
        services.AddScoped<ICompanyProvisioningService, CompanyProvisioningService>();
        services.AddScoped<ISubscriberProvisioningOrchestrator, SubscriberProvisioningOrchestrator>();
        services.AddScoped<ISubscriberIntegrityRepairService, SubscriberIntegrityRepairService>();
        services.AddSingleton<ISecurityMetrics, SecurityMetrics>();
        services.AddSingleton<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<ERP.Application.MasterData.Reconciliation.IMasterDataReconciliationService,
            MasterData.Reconciliation.BusinessPartnerReconciliationService>();
        services.AddScoped<ERP.Application.Modules.Platform.Companies.ICompanyAccessGuard, CompanyAccessGuard>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<IAccountingRepository, AccountingRepository>();
        services.AddScoped<IAccountingSetupRepository, ConfiguracionContableRepository>();
        services.AddScoped<ICuentaContableService, CuentaContableService>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ITaxRateRepository, TaxRateRepository>();
        services.AddScoped<IProductCatalogRepository, ProductCatalogRepository>();
        services.AddScoped<IAccessRepository, AccessRepository>();
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<ISubscriberRepository, SubscriberRepository>();
        services.AddScoped<ISecurityRepository, SecurityRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IGeographyReadRepository, GeographyReadRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IXmlFacturaParser, SriFacturaParser>();
        services.AddScoped<IFileStorage, LocalFileStorage>();

        // SRI Ecuador — switch Simulado/Real via appsettings.json "Sri:UseRealService"
        // En desarrollo/pruebas: Sri:UseRealService = false (simulado, sin certificado real)
        // En producción:         Sri:UseRealService = true  (real, requiere P12 válido)
        var useSriReal = configuration.GetValue<bool>("Sri:UseRealService");
        if (useSriReal)
        {
            services.AddHttpClient("sri").ConfigureHttpClient(c =>
            {
                c.Timeout = TimeSpan.FromSeconds(60);
            });
            services.AddScoped<ERP.Infrastructure.Services.Sri.SriSoapClient>();
            services.AddScoped<ISriFacturaElectronicaService, SriFacturaElectronicaRealService>();
        }
        else
        {
            services.AddScoped<ISriFacturaElectronicaService, SriFacturaElectronicaSimuladoService>();
        }

        services.AddScoped<ISriComprobanteRetentionService, SriWithholdingSimulatedService>();
        services.AddScoped<IRideGeneratorService, ERP.Infrastructure.Services.Sri.RideGeneratorService>();
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
        services.AddScoped<IKardexReportEnqueueService, KardexReportEnqueueService>();
        services.AddHostedService<KardexSnapshotWorker>();
        services.AddHostedService<KardexReportProcessor>();
        services.AddScoped<IStockTransferRepository, StockTransferRepository>();
        services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IAppFeatureRepository, AppFeatureRepository>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ICommercialLimitUsageProvider, MaxCompaniesLimitUsageProvider>();
        services.AddScoped<ICommercialLimitUsageProvider, MaxUsersLimitUsageProvider>();
        services.AddScoped<ICommercialLimitUsageProvider, MaxBranchesLimitUsageProvider>();
        services.AddScoped<ICommercialLimitUsageProvider, MaxWarehousesLimitUsageProvider>();
        services.AddScoped<ICommercialPlanLimitService, CommercialPlanLimitService>();
        services.AddScoped<ISubscriberBillingRepository, SubscriberBillingRepository>();
        services.AddScoped<IBillingGovernanceService, BillingGovernanceService>();
        services.AddScoped<IPaymentProviderAdapter, NullPaymentProviderAdapter>();
        services.AddScoped<DistributedSubscriberEntitlementsSnapshotCache>();
        services.AddScoped<SubscriberEntitlementsPermissionsCacheInvalidator>();
        services.AddScoped<ISubscriberEntitlementsSnapshotCache>(sp =>
            sp.GetRequiredService<DistributedSubscriberEntitlementsSnapshotCache>());
        services.AddScoped<ISubscriberEntitlementsCacheInvalidator>(sp =>
            sp.GetRequiredService<SubscriberEntitlementsPermissionsCacheInvalidator>());
        services.AddScoped<IEntitlementsCacheService, EntitlementsCacheService>();
        services.AddSingleton<IPermissionsCacheDiagnostics, PermissionsCacheDiagnostics>();
        services.Configure<SaasEntitlementsCacheOptions>(
            configuration.GetSection(SaasEntitlementsCacheOptions.SectionName));
        services.AddScoped<ISubscriberEntitlementsService, SubscriberEntitlementsService>();
        services.AddScoped<CommercialCatalogQuery>();
        services.AddScoped<ICommercialCatalogQuery>(sp => sp.GetRequiredService<CommercialCatalogQuery>());
        services.AddScoped<ISaasPublicPlansQuery>(sp => sp.GetRequiredService<CommercialCatalogQuery>());
        services.AddScoped<ICommercialPlansAdminService, CommercialPlansAdminService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddScoped<INavigationMenuReader, NavigationMenuReader>();
        services.AddScoped<SubscriberMenuService>();
        services.AddScoped<ISubscriberSessionMenuResolver>(sp => sp.GetRequiredService<SubscriberMenuService>());
        services.AddScoped<ISubscriberMenuAdminService>(sp => sp.GetRequiredService<SubscriberMenuService>());
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
        services.AddScoped<ISubscriberOnboardingService, SubscriberOnboardingService>();

        // Event-driven foundation — outbox processor
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        return services;
    }
}

