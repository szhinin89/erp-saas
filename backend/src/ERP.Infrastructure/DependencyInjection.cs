using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Common;
using ERP.Domain.Common;
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
using ERP.Domain.Modules.Commercial.Interfaces;
using ERP.Domain.Modules.Fiscal.Interfaces;
using ERP.Domain.Modules.Integration.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Cash.Interfaces;
using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.MasterData;
using ERP.Application.Modules.Fiscal.Integration;
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
using ERP.Infrastructure.Persistence;
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
using ERP.Infrastructure.Ids;
using ERP.Infrastructure.Options;
using ERP.Infrastructure.Persistence.Outbox;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Security;
using ERP.Infrastructure.Observability;
using ERP.Application.Modules.Dashboard;

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
        services.Configure<SnowflakeOptions>(configuration.GetSection(SnowflakeOptions.SectionName));
        services.AddSingleton<ISnowflakeIdGenerator, SnowflakeIdGenerator>();
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

        // ── AR / AP ───────────────────────────────────────────────────────
        services.AddScoped<IArApRepository, ArApRepository>();

        // ── Dashboard KPIs ────────────────────────────────────────────────
        services.AddScoped<IDashboardKpiReader, DashboardKpiReader>();

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
        services.AddSingleton<ERP.Application.Common.IOnboardingCacheInvalidator,
            ERP.Infrastructure.Services.OnboardingCacheInvalidator>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<IAccountingRepository, AccountingRepository>();
        services.AddScoped<IAccountingSetupRepository, AccountingConfigurationRepository>();
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
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IEstablishmentRepository, EstablishmentRepository>();
        services.AddScoped<IEmissionPointRepository, EmissionPointRepository>();
        services.AddScoped<IDocumentSequenceRepository, DocumentSequenceRepository>();
        services.AddScoped<IXmlFacturaParser, SriFacturaParser>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddSingleton<ERP.Application.Common.Interfaces.SRI.IElectronicDocumentBuilder,
            ERP.Infrastructure.Services.Sri.ElectronicDocumentBuilderAdapter>();
        services.AddSingleton<ERP.Application.Common.Interfaces.SRI.IElectronicDocumentSigner,
            ERP.Infrastructure.Services.Sri.ElectronicDocumentSignerAdapter>();

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

        // Subscription Lifecycle Engine — centralized configurable constants
        // Binds from SaaS:Billing (new nested) → falls back to SaaS → flat SubscriptionBilling
        var opts = typeof(ERP.Application.Common.Subscriptions.SubscriptionBillingOptions);
        var billingSection = configuration.GetSection("SaaS:Billing");
        if (!billingSection.Exists())
            billingSection = configuration.GetSection(
                ERP.Application.Common.Subscriptions.SubscriptionBillingOptions.LegacySectionName);

        services.Configure<ERP.Application.Common.Subscriptions.SubscriptionBillingOptions>(billingSection);

        // Renewal look-ahead may be in SaaS:Renewal
        services.PostConfigure<ERP.Application.Common.Subscriptions.SubscriptionBillingOptions>(o =>
        {
            var renewal = configuration.GetSection("SaaS:Renewal");
            if (renewal.Exists())
            {
                var hours = renewal.GetValue<int>("LookAheadHours");
                if (hours > 0) o.RenewalLookAheadHours = hours;
            }
            var cache = configuration.GetSection("SaaS:Cache");
            if (cache.Exists())
            {
                var l1  = cache.GetValue<int>("AccessCacheL1TtlSeconds");
                var l2  = cache.GetValue<int>("AccessCacheL2TtlMinutes");
                var ver = cache.GetValue<int>("AccessCacheVersionTtlDays");
                if (l1  > 0) o.AccessCacheL1TtlSeconds   = l1;
                if (l2  > 0) o.AccessCacheL2TtlMinutes   = l2;
                if (ver > 0) o.AccessCacheVersionTtlDays  = ver;
            }
        });

        services.AddSingleton<ERP.Infrastructure.SaaS.SubscriptionMetrics>();
        services.AddSingleton<ERP.Application.Common.Subscriptions.ISubscriptionAccessCache,
            ERP.Infrastructure.SaaS.HybridSubscriptionAccessCache>();
        services.AddScoped<ERP.Application.Common.Subscriptions.ISubscriptionAccessService,
            ERP.Infrastructure.SaaS.SubscriptionAccessService>();
        services.AddScoped<ERP.Application.Common.Subscriptions.ISubscriptionLifecycleOrchestrator,
            ERP.Infrastructure.SaaS.SubscriptionLifecycleOrchestrator>();
        services.AddScoped<IPaymentProviderAdapter, NullPaymentProviderAdapter>();

        // Billing Flow Architecture (payment providers, renewal, notifications)
        services.AddSingleton<ERP.Infrastructure.SaaS.NullPaymentProvider>();
        services.AddSingleton<ERP.Application.Billing.PaymentProviders.IPaymentProviderFactory,
            ERP.Infrastructure.SaaS.PaymentProviderFactory>();
        services.AddScoped<ERP.Application.Billing.PaymentProviders.IPaymentWebhookProcessor,
            ERP.Infrastructure.SaaS.WebhookEventProcessor>();
        services.AddScoped<ERP.Application.Billing.Renewal.ISubscriptionRenewalService,
            ERP.Infrastructure.SaaS.SubscriptionRenewalService>();
        services.AddScoped<ERP.Application.Billing.Notifications.IBillingNotificationService,
            ERP.Infrastructure.SaaS.NullBillingNotificationService>();
        services.AddScoped<ERP.Application.Billing.Invoices.IBillingInvoiceService,
            ERP.Infrastructure.SaaS.BillingInvoiceService>();
        services.AddScoped<ERP.Application.Common.Features.IFeatureAccessService,
            ERP.Infrastructure.SaaS.FeatureAccessService>();
        services.AddScoped<DistributedSubscriberEntitlementsSnapshotCache>();
        services.AddScoped<SubscriberEntitlementsPermissionsCacheInvalidator>();
        services.AddScoped<ISubscriberEntitlementsSnapshotCache>(sp =>
            sp.GetRequiredService<DistributedSubscriberEntitlementsSnapshotCache>());
        services.AddScoped<ISubscriberEntitlementsCacheInvalidator>(sp =>
            sp.GetRequiredService<SubscriberEntitlementsPermissionsCacheInvalidator>());
        services.AddScoped<IEntitlementsCacheService, EntitlementsCacheService>();
        services.AddSingleton<IPermissionsCacheDiagnostics, PermissionsCacheDiagnostics>();
        services.Configure<SubscriberEntitlementsCacheOptions>(
            configuration.GetSection(SubscriberEntitlementsCacheOptions.SectionName));
        services.AddScoped<ISubscriberEntitlementsService, SubscriberEntitlementsService>();
        services.AddScoped<ERP.Application.Platform.Audit.IPlatformAuditLogger, ERP.Infrastructure.Platform.Audit.PlatformAuditLogger>();
        services.AddScoped<ERP.Application.Platform.Audit.IPlatformAuditReader, ERP.Infrastructure.Platform.Audit.PlatformAuditReader>();
        services.AddScoped<ERP.Application.Platform.Users.IPlatformUsersReader, ERP.Infrastructure.Platform.Users.PlatformUsersReader>();

        // Platform Provisioning services
        services.AddScoped<ERP.Application.Platform.Provisioning.IPlatformProvisioningLockService,
            ERP.Infrastructure.Platform.PlatformProvisioningLockService>();
        services.AddScoped<ERP.Application.Platform.Provisioning.IPlatformProvisioningAuditService,
            ERP.Infrastructure.Platform.PlatformProvisioningAuditService>();
        services.AddScoped<ERP.Application.Platform.Provisioning.IPlatformProvisioningResetService,
            ERP.Infrastructure.Platform.PlatformProvisioningResetService>();
        services.AddScoped<CommercialCatalogQuery>();
        services.AddScoped<ICommercialCatalogQuery>(sp => sp.GetRequiredService<CommercialCatalogQuery>());
        services.AddScoped<IPublicCommercialPlansQuery>(sp => sp.GetRequiredService<CommercialCatalogQuery>());
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
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
        services.AddScoped<IDocumentRelationRepository, DocumentRelationRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<ISalesOriginalBillResolver, SalesOriginalBillResolver>();
        services.AddScoped<ITirillaFacturaService, TirillaFacturaService>();
        services.AddScoped<ICashRepository, CajaRepository>();
        services.AddScoped<IStatementParser, BankStatementCsvParser>();
        services.AddScoped<ICarrierRepository, CarrierRepository>();
        services.AddScoped<IDefaultProfileSeeder, DefaultProfileSeeder>();
        services.AddScoped<ISubscriberOnboardingService, SubscriberOnboardingService>();
        services.AddScoped<ICompanyBootstrapService, CompanyBootstrapService>();

        // Event-driven foundation — outbox processor
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        return services;
    }
}

