using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Dashboard;
using ERP.Application.Navigation;
using ERP.Application.Setup;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Geography.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Menu.Interfaces;
using ERP.Domain.Security.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using ERP.Infrastructure.Access.Caching;
using ERP.Infrastructure.Ids;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Observability;
using ERP.Infrastructure.Options;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Outbox;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.MasterData;
using ERP.Infrastructure.Security;
using ERP.Infrastructure.Seeding;
using ERP.Infrastructure.Seeding.InstallData;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.Configure<InstallDataOptions>(
            configuration.GetSection(InstallDataOptions.SectionName)
        );
        services.Configure<SnowflakeOptions>(
            configuration.GetSection(SnowflakeOptions.SectionName)
        );
        services.AddSingleton<ISnowflakeIdGenerator, SnowflakeIdGenerator>();
        services.AddScoped<IInstallDataBootstrapService, InstallDataBootstrapService>();

        services.AddScoped<PostgreSqlSessionContextInterceptor>();
        services.AddScoped<CompanyTenantInterceptor>();
        services.AddScoped<DbCommandTenantInterceptor>();
        services.AddScoped<NewChildEntityTrackingInterceptor>();
        services.AddDbContext<ErpDbContext>(
            (sp, options) =>
                options
                    .UseNpgsql(
                        configuration.GetConnectionString("DefaultConnection"),
                        b => b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)
                    )
                    .ConfigureWarnings(w =>
                        w.Ignore(
                            Microsoft
                                .EntityFrameworkCore
                                .Diagnostics
                                .RelationalEventId
                                .PendingModelChangesWarning
                        )
                    )
                    .AddInterceptors(
                        sp.GetRequiredService<PostgreSqlSessionContextInterceptor>(),
                        sp.GetRequiredService<CompanyTenantInterceptor>(),
                        sp.GetRequiredService<DbCommandTenantInterceptor>(),
                        sp.GetRequiredService<NewChildEntityTrackingInterceptor>()
                    )
        );

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
        // Motor de firma XAdES-BES (ERP.Infrastructure/Services/Sri) — ya existía sin registrar
        // en DI; único motor de firma del ERP, ElectronicDocuments lo consume sin duplicarlo.
        services.AddSingleton<
            ERP.Application.Common.Interfaces.SRI.IElectronicDocumentSigner,
            ERP.Infrastructure.Services.Sri.ElectronicDocumentSignerAdapter
        >();
        services.AddSingleton<
            ERP.Application.Common.Interfaces.SRI.ISriCertificateInspector,
            ERP.Infrastructure.Services.Sri.SriCertificateInspector
        >();
        // Cliente SOAP del SRI (recepción/autorización/ping) — existía sin registrar en DI
        // ni tener el HttpClient nombrado "sri" configurado; gap real cerrado aquí, no una
        // reimplementación (ver auditoría del refactor de Settings → Electronic Invoicing).
        services.AddHttpClient("sri", client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<ERP.Infrastructure.Services.Sri.SriSoapClient>();
        services.AddScoped<
            ERP.Application.Common.Interfaces.SRI.ISriConnectivityChecker,
            ERP.Infrastructure.Services.Sri.SriConnectivityChecker
        >();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<RefreshTokenRateLimiter>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<
            ERP.Application.Common.Interfaces.IUserAccessRevocationService,
            ERP.Infrastructure.Services.UserAccessRevocationService
        >();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IPasswordResetLinkSender, LoggingPasswordResetLinkSender>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICurrentTenant, CurrentTenantService>();
        services.AddScoped<ICurrentCompany, CurrentCompanyService>();
        services.AddScoped<ICurrentBranch, CurrentBranchService>();
        services.AddScoped<
            ICurrentCashSession,
            ERP.Infrastructure.Services.CurrentCashSessionService
        >();
        services.AddScoped<ISessionContext, HttpSessionContext>();
        services.AddScoped<IDbSessionContextApplicator, DbSessionContextApplicator>();
        services.AddScoped<IOperationalContext, OperationalContextService>();
        services.AddScoped<IMembershipAuthority, MembershipAuthority>();

        // ── Dashboard KPIs ────────────────────────────────────────────────────
        services.AddScoped<IDashboardKpiReader, DashboardKpiReader>();

        // ── MasterData BC ─────────────────────────────────────────────────────
        services.AddScoped<IBusinessPartnerRepository, BusinessPartnerRepository>();
        services.AddScoped<ILegalEntityTypeRepository, LegalEntityTypeRepository>();
        services.AddScoped<IBusinessPartnerRoleRepository, BusinessPartnerRoleRepository>();
        services.AddScoped<IBusinessPartnerLocationRepository, BusinessPartnerLocationRepository>();
        services.AddScoped<IBusinessPartnerContactRepository, BusinessPartnerContactRepository>();
        services.AddScoped<IPaymentTermRepository, PaymentTermRepository>();

        // ── MasterData BC — catálogos de clasificación BusinessPartner (CLASS-BP-CATALOGS-01) ──
        services.AddScoped<ICustomerCategoryRepository, CustomerCategoryRepository>();
        services.AddScoped<ICustomerSegmentRepository, CustomerSegmentRepository>();
        services.AddScoped<ICustomerCreditRatingRepository, CustomerCreditRatingRepository>();
        services.AddScoped<ILoyaltyTierRepository, LoyaltyTierRepository>();
        services.AddScoped<ICustomerInvoiceFormatRepository, CustomerInvoiceFormatRepository>();
        services.AddScoped<
            ICustomerClassificationRepository,
            CustomerClassificationRepository
        >();
        services.AddScoped<ISupplierCategoryRepository, SupplierCategoryRepository>();
        services.AddScoped<ISupplierTypeRepository, SupplierTypeRepository>();
        services.AddScoped<ISupplierRiskRepository, SupplierRiskRepository>();
        services.AddScoped<ISupplierRatingRepository, SupplierRatingRepository>();
        services.AddScoped<IPrimaryGoodTypeRepository, PrimaryGoodTypeRepository>();
        services.AddScoped<ISupplierSegmentRepository, SupplierSegmentRepository>();
        services.AddScoped<
            ICompanyBpTradingSettingsRepository,
            CompanyBpTradingSettingsRepository
        >();
        services.AddScoped<DistributedPermissionsCacheService>();
        services.AddScoped<ResilientPermissionsCacheService>();
        services.AddScoped<IPermissionsCacheBackend>(sp =>
            sp.GetRequiredService<ResilientPermissionsCacheService>()
        );
        services.AddScoped<IPermissionsCacheService>(sp =>
            sp.GetRequiredService<ResilientPermissionsCacheService>()
        );
        services.AddScoped<IPermissionsCacheInvalidator, PermissionsCacheInvalidator>();
        services.AddScoped<ICompanyProvisioningService, CompanyProvisioningService>();
        services.AddSingleton<ISecurityMetrics, SecurityMetrics>();
        services.AddSingleton<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<
            ERP.Application.MasterData.Reconciliation.IMasterDataReconciliationService,
            MasterData.Reconciliation.BusinessPartnerReconciliationService
        >();
        services.AddScoped<
            ERP.Application.Modules.Companies.ICompanyAccessGuard,
            CompanyAccessGuard
        >();
        services.AddScoped<
            ERP.Application.Modules.Branches.IBranchAccessGuard,
            BranchAccessGuard
        >();
        services.AddScoped<
            ERP.Application.Modules.Branches.IInterBranchAccessGuard,
            InterBranchAccessGuard
        >();
        services.AddScoped<
            ERP.Application.Modules.Caja.ICashRegisterUsageGuard,
            CashRegisterUsageGuard
        >();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICurrentUser, CurrentUserService>();

        // ── Media ─────────────────────────────────────────────────────────────
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<
            ERP.Domain.Modules.Media.Interfaces.IMediaFileRepository,
            ERP.Infrastructure.Persistence.Repositories.MediaFileRepository
        >();
        services.AddScoped<
            ERP.Application.Modules.Media.IImageValidationService,
            ERP.Application.Modules.Media.ImageValidationService
        >();
        services.AddScoped<
            ERP.Application.Modules.Media.IMediaService,
            ERP.Application.Modules.Media.MediaService
        >();
        // ── SRI Catalog Resolver (cross-module) ───────────────────────────────
        services.AddScoped<
            ERP.Application.Common.ISriCatalogResolver,
            ERP.Infrastructure.Persistence.Catalogs.SriCatalogResolver
        >();
        services.AddScoped<
            ERP.Application.Common.Services.ISriDocTypeCatalogResolver,
            ERP.Infrastructure.Persistence.Services.SriDocTypeCatalogResolver
        >();
        services.AddScoped<
            ERP.Domain.Modules.SriCatalogs.Interfaces.ISriCatalogLookupRepository,
            ERP.Infrastructure.Persistence.Repositories.SriCatalogs.SriCatalogLookupRepository
        >();

        // ── Items BC ──────────────────────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Items.Interfaces.IItemRepository,
            ERP.Infrastructure.Persistence.Repositories.Items.ItemRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Items.Interfaces.IItemCatalogRepository,
            ERP.Infrastructure.Persistence.Repositories.Items.ItemCatalogRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Items.Interfaces.ICategoryNodeRepository,
            ERP.Infrastructure.Persistence.Repositories.Items.CategoryNodeRepository
        >();
        services.AddScoped<
            ERP.Application.Items.UseCases.AttributeGroups.IAttributeGroupRepository,
            ERP.Infrastructure.Persistence.Repositories.Items.AttributeGroupRepository
        >();
        services.AddScoped<
            ERP.Application.Items.UseCases.AttributeDefinitions.IAttributeDefinitionRepository,
            ERP.Infrastructure.Persistence.Repositories.Items.AttributeDefinitionRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Items.Interfaces.IItemTypeRepository,
            ERP.Infrastructure.Persistence.Repositories.Items.ItemTypeRepository
        >();

        services.AddScoped<IAccessRepository, AccessRepository>();
        services.AddScoped<
            ERP.Domain.Access.Interfaces.IUserSessionRepository,
            ERP.Infrastructure.Persistence.Repositories.UserSessionRepository
        >();
        services.AddScoped<
            ERP.Domain.Access.Interfaces.ICompanyUserBranchRepository,
            ERP.Infrastructure.Persistence.Repositories.CompanyUserBranchRepository
        >();
        services.AddScoped<
            ERP.Domain.Access.Interfaces.ICompanyUserPreferencesRepository,
            ERP.Infrastructure.Persistence.Repositories.CompanyUserPreferencesRepository
        >();
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ISecurityRepository, SecurityRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IGeographyReadRepository, GeographyReadRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped(
            typeof(ERP.Application.Audit.IAuditWriter<>),
            typeof(ERP.Infrastructure.Audit.EfAuditWriter<>)
        );
        services.AddScoped(
            typeof(ERP.Application.Audit.IAuditReader<>),
            typeof(ERP.Infrastructure.Audit.EfAuditReader<>)
        );
        services.AddScoped<
            ERP.Application.Audit.IAuditContext,
            ERP.Infrastructure.Audit.HttpAuditContext
        >();
        services.AddScoped<
            ERP.Application.Audit.IAuditService,
            ERP.Infrastructure.Audit.AuditService
        >();

        // ── Codes BC (Building Block transversal de generación de códigos) ─────
        services.AddSingleton<
            ERP.Application.Codes.IQrCodeGenerator,
            ERP.Infrastructure.Codes.QrCodeGenerator
        >();
        services.AddSingleton<
            ERP.Application.Codes.Barcodes.IBarcodeGenerator,
            ERP.Infrastructure.Codes.Barcodes.Code128BarcodeGenerator
        >();

        services.AddScoped<IWarehouseRepository, WarehouseRepository>();

        // ── Pricing BC ───────────────────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Pricing.Interfaces.IPriceListRepository,
            ERP.Infrastructure.Persistence.Repositories.Pricing.PriceListRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Pricing.Interfaces.IPricingRuleRepository,
            ERP.Infrastructure.Persistence.Repositories.Pricing.PricingRuleRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Pricing.Interfaces.IPriceListItemRepository,
            ERP.Infrastructure.Persistence.Repositories.Pricing.PriceListItemRepository
        >();

        // ── Accounting BC (ADR-026 — fundamentos) ───────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Accounting.Interfaces.IAccountRepository,
            ERP.Infrastructure.Accounting.Repositories.AccountRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Accounting.Interfaces.IAccountingPeriodRepository,
            ERP.Infrastructure.Accounting.Repositories.AccountingPeriodRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Accounting.Interfaces.IJournalEntryRepository,
            ERP.Infrastructure.Accounting.Repositories.JournalEntryRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Accounting.Interfaces.IPostingRuleRepository,
            ERP.Infrastructure.Accounting.Repositories.PostingRuleRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Accounting.Interfaces.IJournalEntrySequenceRepository,
            ERP.Infrastructure.Accounting.Repositories.JournalEntrySequenceRepository
        >();
        services.AddScoped<
            ERP.Application.Modules.Accounting.Posting.IPostingEngine,
            ERP.Application.Modules.Accounting.Posting.PostingEngine
        >();

        services.AddScoped<
            ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository,
            ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments.ElectronicDocumentRepository
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentIssuer,
            ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentIssuer
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentDataProviderResolver,
            ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentDataProviderResolver
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentDataProvider,
            ERP.Application.Modules.Sales.Services.SalesInvoiceElectronicDocumentDataProvider
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentDataProvider,
            ERP.Application.Modules.Sales.Services.SalesReturnCreditNoteDataProvider
        >();
        // Los demás módulos (Purchases, Inventory, ...) añadirán su propio
        // IElectronicDocumentDataProvider aquí cuando implementen su proveedor.

        // ── Estrategia de emisión de Ventas (Electronic vs Physical) ────────────
        services.AddScoped<
            ERP.Application.Modules.Sales.Services.ISalesInvoiceEmissionStrategy,
            ERP.Application.Modules.Sales.Services.ElectronicSalesInvoiceEmissionStrategy
        >();
        services.AddScoped<
            ERP.Application.Modules.Sales.Services.ISalesInvoiceEmissionStrategy,
            ERP.Application.Modules.Sales.Services.PhysicalSalesInvoiceEmissionStrategy
        >();
        services.AddScoped<
            ERP.Application.Modules.Sales.Services.ISalesInvoiceEmissionStrategyResolver,
            ERP.Application.Modules.Sales.Services.SalesInvoiceEmissionStrategyResolver
        >();

        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.ISourceDocumentSummaryProviderResolver,
            ERP.Application.Modules.ElectronicDocuments.Services.SourceDocumentSummaryProviderResolver
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.ISourceDocumentSummaryProvider,
            ERP.Application.Modules.Sales.Services.SalesSourceDocumentSummaryProvider
        >();
        // Los demás módulos añadirán su propio ISourceDocumentSummaryProvider aquí — el Monitor
        // de Documentos Electrónicos nunca debe volver a hardcodear "Sales".

        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.XmlBuilders.IElectronicDocumentXmlBuilderResolver,
            ERP.Application.Modules.ElectronicDocuments.XmlBuilders.ElectronicDocumentXmlBuilderResolver
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.XmlBuilders.IElectronicDocumentXmlBuilder,
            ERP.Application.Modules.ElectronicDocuments.XmlBuilders.InvoiceXmlBuilder
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.XmlBuilders.IElectronicDocumentXmlBuilder,
            ERP.Application.Modules.ElectronicDocuments.XmlBuilders.CreditNoteXmlBuilder
        >();
        // Los demás tipos de comprobante (DebitNote, Retention, ShippingGuide, ...) añadirán su
        // propio IElectronicDocumentXmlBuilder aquí — nunca tocar el resolver.

        // Fase 9: mapeo real VAT→"2"/ICE→"3" (constante regulatoria SRI, no catálogo de BD —
        // ver justificación en SriTaxCategoryCodeResolver). Reemplaza al placeholder que
        // siempre devolvía null (PendingCatalogSriTaxCategoryCodeResolver, eliminado).
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.XmlBuilders.ISriTaxCategoryCodeResolver,
            ERP.Infrastructure.Services.ElectronicDocuments.SriTaxCategoryCodeResolver
        >();

        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentSigningService,
            ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentSigningService
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicInvoicing.Services.ISriCertificateStatusResolver,
            ERP.Application.Modules.ElectronicInvoicing.Services.SriCertificateStatusResolver
        >();

        // Fase 8: adapta SriSoapClient (ya registrado arriba) a la interfaz que consume
        // Application — Application nunca conoce SriSoapClient/HttpClient directamente.
        services.AddScoped<
            ERP.Application.Common.Interfaces.SRI.ISriReceptionClient,
            ERP.Infrastructure.Services.Sri.SriReceptionClient
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentReceptionService,
            ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentReceptionService
        >();

        // Fase 9: mismo patrón adaptador para la consulta de autorización — reutiliza el mismo
        // SriSoapClient (ya registrado arriba), nunca un segundo cliente SOAP.
        services.AddScoped<
            ERP.Application.Common.Interfaces.SRI.ISriAuthorizationClient,
            ERP.Infrastructure.Services.Sri.SriAuthorizationClient
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentAuthorizationService,
            ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentAuthorizationService
        >();

        // Estrategia de nombres (pura, sin I/O) + servicio de almacenamiento (único consumidor
        // de IFileStorage para XML de ElectronicDocuments — ver auditoría de la Fase 7).
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentStorageNamingStrategy,
            ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentStorageNamingStrategy
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentXmlStorageService,
            ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentXmlStorageService
        >();

        // Singleton: el XmlSchemaSet compilado es seguro para lecturas concurrentes y cachearlo
        // una sola vez por proceso evita recompilar el XSD en cada validación.
        services.AddSingleton<
            ERP.Application.Modules.ElectronicDocuments.SchemaValidation.IXmlSchemaProvider,
            ERP.Infrastructure.Services.ElectronicDocuments.EmbeddedXmlSchemaProvider
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.SchemaValidation.IElectronicDocumentSchemaValidatorResolver,
            ERP.Application.Modules.ElectronicDocuments.SchemaValidation.ElectronicDocumentSchemaValidatorResolver
        >();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.SchemaValidation.IElectronicDocumentSchemaValidator,
            ERP.Application.Modules.ElectronicDocuments.SchemaValidation.InvoiceXmlSchemaValidator
        >();
        // ADR-031 (Fase 11, P0-01): CreditNote V1.1.0 activada — mismo patrón, ningún cambio al
        // resolver ni a IXmlSchemaProvider.
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.SchemaValidation.IElectronicDocumentSchemaValidator,
            ERP.Application.Modules.ElectronicDocuments.SchemaValidation.CreditNoteXmlSchemaValidator
        >();
        // Los demás tipos de comprobante añadirán su propio IElectronicDocumentSchemaValidator
        // aquí — nunca tocar el resolver ni IXmlSchemaProvider.

        services.AddScoped<
            ERP.Domain.Modules.Pricing.Services.IPricingAdjustmentStrategy,
            ERP.Domain.Modules.Pricing.Services.FixedPriceStrategy
        >();
        services.AddScoped<
            ERP.Domain.Modules.Pricing.Services.IPricingAdjustmentStrategy,
            ERP.Domain.Modules.Pricing.Services.PercentDiscountStrategy
        >();
        services.AddScoped<
            ERP.Domain.Modules.Pricing.Services.IPricingAdjustmentStrategy,
            ERP.Domain.Modules.Pricing.Services.PercentMarkupStrategy
        >();
        services.AddScoped<
            ERP.Domain.Modules.Pricing.Services.IPricingAdjustmentStrategy,
            ERP.Domain.Modules.Pricing.Services.FixedAdjustmentStrategy
        >();
        services.AddScoped<
            ERP.Application.Modules.Pricing.Services.IPricingAdjustmentStrategyResolver,
            ERP.Application.Modules.Pricing.Services.PricingAdjustmentStrategyResolver
        >();
        services.AddScoped<
            ERP.Application.Modules.Pricing.Services.IPricingResolver,
            ERP.Application.Modules.Pricing.Services.PricingResolver
        >();

        // ── Ride BC (walking skeleton — Fase 4 del plan de implementación, ADR-025) ──────────
        services.AddScoped<
            ERP.Domain.Modules.Ride.Interfaces.IRidePdfDocumentRepository,
            ERP.Infrastructure.Persistence.Repositories.Ride.RidePdfDocumentRepository
        >();

        services.AddScoped<
            ERP.Application.Modules.Ride.Parsers.IRideXmlParserResolver,
            ERP.Application.Modules.Ride.Parsers.RideXmlParserResolver
        >();
        services.AddScoped<
            ERP.Application.Modules.Ride.Parsers.IRideXmlParser,
            ERP.Application.Modules.Ride.Parsers.InvoiceRideXmlParser
        >();
        // ADR-031 addendum (Fase 12, P0-01): Nota de Crédito — mismo patrón, ningún cambio al
        // resolver.
        services.AddScoped<
            ERP.Application.Modules.Ride.Parsers.IRideXmlParser,
            ERP.Application.Modules.Ride.Parsers.CreditNoteRideXmlParser
        >();
        // Los demás tipos de comprobante (DebitNote, Retention, ShippingGuide, ...) añadirán su
        // propio IRideXmlParser aquí — nunca tocar el resolver.

        services.AddScoped<
            ERP.Application.Modules.Ride.Templates.IRideTemplateResolver,
            ERP.Application.Modules.Ride.Templates.RideTemplateResolver
        >();
        services.AddScoped<
            ERP.Application.Modules.Ride.Templates.IRideTemplate,
            ERP.Application.Modules.Ride.Templates.DefaultInvoiceRideTemplate
        >();
        // ADR-031 addendum (Fase 12, P0-01): Nota de Crédito — mismo patrón, ningún cambio al
        // resolver.
        services.AddScoped<
            ERP.Application.Modules.Ride.Templates.IRideTemplate,
            ERP.Application.Modules.Ride.Templates.CreditNoteRideTemplate
        >();
        // Las demás plantillas añadirán su propia IRideTemplate aquí — nunca tocar el resolver.

        services.AddScoped<
            ERP.Application.Modules.Ride.Services.IRideSourceXmlProvider,
            ERP.Infrastructure.Ride.ElectronicDocumentsAdapter.ElectronicDocumentRideSourceXmlProvider
        >();

        // Fase 7: renderer real — reemplaza a NullRideRenderer (Fase 4), eliminado.
        services.AddScoped<
            ERP.Application.Modules.Ride.Rendering.IRideRenderer,
            ERP.Infrastructure.Ride.Rendering.QuestPdfRideRenderer
        >();

        // Fase 8: branding y storage definitivos — reemplazan a NullRideBrandingProvider y
        // NullRidePdfStorageService (Fase 4), eliminados.
        // CONFIG-FOUNDATION-P1-02: CompanyBrandingRideProvider (antes OrgSettingsRideBrandingProvider)
        // ya no lee org_settings directamente — delega en ICompanyBrandingResolver, la fuente
        // única de marca de empresa (Company Branding es owner; RIDE es consumidor).
        services.AddScoped<
            ERP.Application.Modules.Ride.Branding.IRideBrandingProvider,
            ERP.Infrastructure.Ride.Branding.CompanyBrandingRideProvider
        >();
        services.AddScoped<
            ERP.Application.Modules.Ride.Storage.IRidePdfStorageNamingStrategy,
            ERP.Infrastructure.Ride.Storage.RidePdfStorageNamingStrategy
        >();
        services.AddScoped<
            ERP.Application.Modules.Ride.Storage.IRidePdfStorageService,
            ERP.Infrastructure.Ride.Storage.RidePdfStorageService
        >();

        // Generador real de QR: adaptador delgado sobre el Building Block transversal
        // ERP.Application.Codes.IQrCodeGenerator (ver registro más abajo) — Ride no implementa
        // ninguna lógica de codificación QR propia.
        services.AddScoped<
            ERP.Application.Modules.Ride.Qr.IRideQrCodeGenerator,
            ERP.Infrastructure.Ride.Qr.RideQrCodeGenerator
        >();

        // Generador real de código de barras Code128: mismo patrón de adaptador delgado sobre
        // ERP.Application.Codes.Barcodes.IBarcodeGenerator (ver registro arriba, bloque Codes BC).
        services.AddScoped<
            ERP.Application.Modules.Ride.Barcodes.IRideBarcodeGenerator,
            ERP.Infrastructure.Ride.Barcodes.RideBarcodeGenerator
        >();

        // Fase 5: implementaciones reales de orquestación y cache — reemplazan a los stubs de
        // Fase 4 (NullRideCacheStrategy, NullRideContentHasher, NullRideDocumentService, eliminados).
        services.AddScoped<
            ERP.Application.Modules.Ride.Services.IRideCacheStrategy,
            ERP.Application.Modules.Ride.Services.RideCacheStrategy
        >();
        services.AddScoped<
            ERP.Application.Modules.Ride.Services.IRideContentHasher,
            ERP.Application.Modules.Ride.Services.RideContentHasher
        >();
        services.AddScoped<ERP.Application.Modules.Ride.Services.RidePipeline>();
        services.AddScoped<
            ERP.Application.Modules.Ride.Services.IRideDocumentService,
            ERP.Application.Modules.Ride.Services.RideDocumentService
        >();

        // ── Company BC (sequences) ───────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Company.Interfaces.IDocumentSequenceRepository,
            ERP.Infrastructure.Persistence.Repositories.DocumentSequenceRepository
        >();

        // ── Inventory BC (stock) ─────────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Inventory.Interfaces.IStockRepository,
            ERP.Infrastructure.Persistence.Repositories.Inventory.StockRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Inventory.Interfaces.IStockAdjustmentRepository,
            ERP.Infrastructure.Persistence.Repositories.Inventory.StockAdjustmentRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Inventory.Interfaces.IStockTransferRepository,
            ERP.Infrastructure.Persistence.Repositories.Inventory.StockTransferRepository
        >();
        services.AddScoped<
            ERP.Application.Common.Interfaces.IAverageCostService,
            ERP.Infrastructure.Services.AverageCostService
        >();

        // ── Purchases BC ─────────────────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Purchases.Interfaces.IPurchaseInvoiceRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseInvoiceRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Purchases.Interfaces.IPurchasePayableRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.PurchasePayableRepository
        >();
        services.AddScoped<ERP.Infrastructure.Persistence.Services.SriTaxResolver>();
        services.AddScoped<ERP.Application.Common.Services.ISriTaxResolver>(sp =>
            sp.GetRequiredService<ERP.Infrastructure.Persistence.Services.SriTaxResolver>()
        );
        services.AddScoped<ERP.Application.Modules.Purchases.Services.ISriTaxResolver>(sp =>
            sp.GetRequiredService<ERP.Infrastructure.Persistence.Services.SriTaxResolver>()
        );
        services.AddScoped<
            ERP.Application.Modules.Purchases.Services.IRetentionCodeResolver,
            ERP.Infrastructure.Persistence.Services.RetentionCodeResolver
        >();
        services.AddScoped<
            ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces.IPurchaseReceptionParser,
            ERP.Infrastructure.Modules.Purchases.PurchaseReception.PurchaseInvoiceTxtParser
        >();
        services.AddScoped<
            ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces.IPurchaseReceptionVerifier,
            ERP.Infrastructure.Modules.Purchases.PurchaseReception.PurchaseReceptionVerifier
        >();
        services.AddScoped<
            ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces.IPurchaseReceptionDocumentRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseReceptionDocumentRepository
        >();
        // P0-02: PurchaseReturn + SupplierCredit (Fase 2 — persistencia, sin casos de uso todavía).
        services.AddScoped<
            ERP.Domain.Modules.Purchases.Interfaces.IPurchaseReturnRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseReturnRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Purchases.Interfaces.ISupplierCreditRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.SupplierCreditRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Purchases.Interfaces.IPurchaseReturnSequenceRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseReturnSequenceRepository
        >();
        // FLOW-READY-02C.2: PurchaseCreditNote (descuento/promoción) — Application/API.
        services.AddScoped<
            ERP.Domain.Modules.Purchases.Interfaces.IPurchaseCreditNoteRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseCreditNoteRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Finance.Interfaces.ICompanyFinancialDestinationRepository,
            ERP.Infrastructure.Persistence.Repositories.Finance.CompanyFinancialDestinationRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Finance.Interfaces.ISupplierCreditRefundTransactionRepository,
            ERP.Infrastructure.Persistence.Repositories.Finance.SupplierCreditRefundTransactionRepository
        >();
        // Fase 3: reutiliza ISriAuthorizationClient (ya registrado para ElectronicDocuments) — nunca un segundo cliente SOAP.
        services.AddScoped<
            ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces.ISriReceptionXmlProvider,
            ERP.Infrastructure.Modules.Purchases.PurchaseReception.SriReceptionXmlProvider
        >();
        services.AddScoped<
            ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing.IPurchaseXmlDraftParser,
            ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing.PurchaseXmlDraftParser
        >();
        // Única implementación de "XML -> líneas + Item Matching" — compartida por la descarga
        // inicial y la reconstrucción transparente del snapshot dentro de CreatePurchaseReceptionDraft.
        services.AddScoped<
            ERP.Application.Modules.Purchases.PurchaseReception.Services.IPurchaseReceptionDetailProcessor,
            ERP.Application.Modules.Purchases.PurchaseReception.Services.PurchaseReceptionDetailProcessor
        >();

        // Item Matching (Inventory) — motor de conciliación de líneas de PurchaseReception contra el
        // catálogo de Items; consume IItemRepository, no crea un repositorio propio.
        services.AddScoped<
            ERP.Application.Modules.Inventory.ItemMatching.Services.IItemMatchFinder,
            ERP.Application.Modules.Inventory.ItemMatching.Services.ItemMatchFinder
        >();
        services.AddScoped<
            ERP.Application.Modules.Inventory.ItemMatching.Services.IItemMatchConfirmationService,
            ERP.Application.Modules.Inventory.ItemMatching.Services.ItemMatchConfirmationService
        >();

        // ── Sales BC ─────────────────────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Sales.Interfaces.ISalesInvoiceRepository,
            ERP.Infrastructure.Persistence.Repositories.Sales.SalesInvoiceRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Sales.Interfaces.IPaymentMethodRepository,
            ERP.Infrastructure.Persistence.Repositories.Sales.PaymentMethodRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Sales.Interfaces.ISalesReceivableRepository,
            ERP.Infrastructure.Persistence.Repositories.Sales.SalesReceivableRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Sales.Interfaces.ISalesReturnRepository,
            ERP.Infrastructure.Persistence.Repositories.Sales.SalesReturnRepository
        >();
        services.AddScoped<
            ERP.Application.Modules.Sales.IInvoiceItemSearchRepository,
            ERP.Infrastructure.Persistence.Repositories.Sales.InvoiceItemSearchRepository
        >();
        services.AddScoped<
            ERP.Application.Common.Services.ICompanyClock,
            ERP.Infrastructure.Persistence.Services.CompanyClock
        >();

        // ── Caja BC ──────────────────────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository,
            ERP.Infrastructure.Persistence.Repositories.Caja.CashRegisterRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Caja.Interfaces.ICashSessionRepository,
            ERP.Infrastructure.Persistence.Repositories.Caja.CashSessionRepository
        >();

        // ── Company Config ───────────────────────────────────────────────────
        services.AddScoped<
            ERP.Application.Modules.Companies.UseCases.DecimalConfig.IDecimalConfigRepository,
            ERP.Infrastructure.Persistence.Repositories.CompanyConfig.DecimalConfigRepository
        >();

        // ── Finance BC ───────────────────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Modules.Finance.Interfaces.ICreditTermRepository,
            ERP.Infrastructure.Persistence.Repositories.Finance.CreditTermRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Finance.Interfaces.IPaymentRepository,
            ERP.Infrastructure.Persistence.Repositories.Finance.PaymentRepository
        >();
        services.AddScoped<IAppFeatureRepository, AppFeatureRepository>();
        services.AddSingleton<IPermissionsCacheDiagnostics, PermissionsCacheDiagnostics>();
        services.AddScoped<INavigationMenuReader, NavigationMenuReader>();
        services.AddScoped<INavigationMenuAdminService, NavigationMenuAdminService>();
        services.AddScoped<
            ERP.Application.Navigation.INavigationBuilder,
            ERP.Infrastructure.Persistence.Navigation.NavigationBuilder
        >();
        services.AddScoped<IDefaultProfileSeeder, DefaultProfileSeeder>();
        services.AddScoped<ICompanyBootstrapService, CompanyBootstrapOrchestrator>();
        services.AddScoped<ICompanyBootstrapStep, OrganizationBootstrapStep>();
        services.AddScoped<ICompanyBootstrapStep, ElectronicDocumentsBootstrapStep>();
        services.AddScoped<ICompanyBootstrapStep, InventoryBootstrapStep>();
        services.AddScoped<MasterDataClassificationSeeder>();
        services.AddScoped<ICompanyBootstrapStep, MasterDataClassificationBootstrapStep>();
        services.AddScoped<MasterDataClassificationBackfillService>();
        services.AddScoped<ICompanyBootstrapStep, SalesBootstrapStep>();
        services.AddScoped<ICompanyBootstrapStep, CajaBootstrapStep>();
        services.AddScoped<ICompanyBootstrapStep, AccessBootstrapStep>();

        // ── Global bootstrap (una sola vez por instalación, cada arranque) ─────
        services.AddScoped<ERP.Infrastructure.Persistence.Navigation.NavigationSyncService>();
        services.AddScoped<
            ERP.Infrastructure.Seeding.Global.IGlobalBootstrapOrchestrator,
            ERP.Infrastructure.Seeding.Global.GlobalBootstrapOrchestrator
        >();
        services.AddScoped<ERP.Infrastructure.Seeding.E2E.E2ESeedService>();
        services.AddScoped<
            ERP.Infrastructure.Seeding.Global.IGlobalBootstrapStep,
            ERP.Infrastructure.Seeding.Global.Steps.NavigationBootstrapStep
        >();
        services.AddScoped<
            ERP.Infrastructure.Seeding.Global.IGlobalBootstrapStep,
            ERP.Infrastructure.Seeding.Global.Steps.InstallDataBootstrapStep
        >();
        services.AddScoped<IEstablishmentRepository, EstablishmentRepository>();
        services.AddScoped<IEmissionPointRepository, EmissionPointRepository>();

        // ── Inventory BC ──────────────────────────────────────────────────────
        services.AddScoped<ERP.Domain.Modules.Inventory.Interfaces.ILotRepository, LotRepository>();
        services.AddScoped<
            ERP.Domain.Modules.Inventory.Interfaces.ISerialRepository,
            SerialRepository
        >();

        // ── SRI validation ───────────────────────────────────────────────────
        services.AddScoped<
            ERP.Application.Common.IIdentificationUsageValidator,
            ERP.Infrastructure.Services.IdentificationUsageValidator
        >();

        // ── Configuration BC ──────────────────────────────────────────────────
        services.AddScoped<
            ERP.Domain.Configuration.Interfaces.ISriSettingsRepository,
            SriSettingsRepository
        >();
        services.AddScoped<
            ERP.Domain.Configuration.Interfaces.IOrgSettingsRepository,
            ERP.Infrastructure.Persistence.Repositories.Configuration.OrgSettingsRepository
        >();
        services.AddScoped<
            ERP.Domain.Configuration.Interfaces.IOrgConfigResolver,
            OrgConfigResolver
        >();
        services.AddScoped<
            ERP.Domain.Modules.Sales.Interfaces.ISalesFiscalPolicyResolver,
            ERP.Infrastructure.Services.SalesFiscalPolicyResolver
        >();
        services.AddScoped<
            ERP.Domain.Modules.Company.Interfaces.ICompanyBrandingResolver,
            ERP.Infrastructure.Services.CompanyBrandingResolver
        >();
        services.AddScoped<
            ERP.Domain.Modules.Sales.Interfaces.IInvoiceDefaultsResolver,
            ERP.Infrastructure.Services.InvoiceDefaultsResolver
        >();
        services.AddScoped<
            ERP.Domain.Modules.Items.Interfaces.ICatalogConfigurationResolver,
            ERP.Infrastructure.Services.CatalogConfigurationResolver
        >();
        services.AddScoped<
            ERP.Domain.Configuration.Interfaces.IConfigurationChangeLogger,
            ERP.Infrastructure.Services.ConfigurationChangeLogger
        >();
        services.AddScoped<
            ERP.Domain.Configuration.Interfaces.IConfigurationChangeLogQueryRepository,
            ERP.Infrastructure.Persistence.Repositories.Configuration.ConfigurationChangeLogQueryRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Company.Interfaces.ICompanyOperationalReadinessResolver,
            ERP.Infrastructure.Services.CompanyOperationalReadinessResolver
        >();

        // ── Setup BC ──────────────────────────────────────────────────────────
        services.AddScoped<ISystemSetupRepository, SystemSetupRepository>();
        services.AddScoped<
            ERP.Application.Setup.IFirstRunSetupService,
            ERP.Infrastructure.Setup.FirstRunSetupService
        >();

        // Event-driven foundation — outbox processor
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        return services;
    }
}
