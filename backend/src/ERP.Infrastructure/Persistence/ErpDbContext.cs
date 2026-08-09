using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Common;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Geography.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Media.Entities;
using ERP.Domain.Modules.Menu.Entities;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Navigation.Entities;
using ERP.Domain.Security.Entities;
using ERP.Domain.Setup;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public class ErpDbContext : DbContext
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly IPublisher _publisher;

    // Identidad por referencia de toda entidad que ChangeTracker haya empezado a
    // rastrear a partir de un resultado de query en ESTE DbContext. Alimenta a
    // NewChildEntityTrackingInterceptor: una entidad que nunca pasó por aquí no
    // tiene snapshot real de BD en este contexto, lo cual es condición necesaria
    // (no suficiente por sí sola) para tratarla como recién creada. Ver
    // AI-RULES — invariante "no Attach/Update de entidades detached con ID real"
    // (gate: NewChildEntityTrackingArchitectureTests).
    private readonly HashSet<object> _queryTrackedEntities = new(
        ReferenceEqualityComparer.Instance
    );

    public ErpDbContext(
        DbContextOptions<ErpDbContext> options,
        ICurrentTenant currentTenant,
        IPublisher publisher,
        ICurrentCompany currentCompany
    )
        : base(options)
    {
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _publisher = publisher;

        ChangeTracker.Tracked += (_, e) =>
        {
            if (e.FromQuery)
                _queryTrackedEntities.Add(e.Entry.Entity);
        };
    }

    internal bool WasTrackedFromQuery(object entity) => _queryTrackedEntities.Contains(entity);

    internal Guid FilterTenantId => _currentTenant.TenantId;
    internal Guid FilterCompanyId => _currentCompany.CompanyId;
    internal bool FilterHasCompanyContext => _currentCompany.HasCompanyContext;

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        var entitiesWithEvents = ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
        foreach (var entity in entitiesWithEvents)
            entity.ClearDomainEvents();

        foreach (var @event in domainEvents)
        {
            var metadataJson = OutboxMetadataFactory.Build(@event);
            OutboxMessages.Add(OutboxMessage.From(@event, metadataJson));
        }

        // Ambas escrituras (estado inicial + side-effects de domain event handlers
        // publicados in-process) deben ser atómicas: si la segunda falla, la
        // primera no puede quedar comprometida. Si ya existe una transacción
        // ambiente (p. ej. IUnitOfWork del caller), la reutilizamos y dejamos
        // el commit/rollback en manos del dueño de esa transacción.
        var ownsTransaction = Database.CurrentTransaction is null;
        var tx = ownsTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;

        // La corrección de entidades hijas mal clasificadas (Modified en lugar de
        // Added tras ser descubiertas por fixup de navegación) vive en
        // NewChildEntityTrackingInterceptor — se dispara automáticamente en cada
        // base.SaveChangesAsync() vía SavingChanges/SavingChangesAsync, sin
        // necesidad de invocación manual aquí. Ver ADR correspondiente.
        try
        {
            int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            foreach (var @event in domainEvents)
                await _publisher.Publish((INotification)@event, cancellationToken);

            if (ChangeTracker.HasChanges())
                result += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (ownsTransaction)
                await tx!.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            if (ownsTransaction)
                await tx!.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (ownsTransaction)
                await tx!.DisposeAsync();
        }
    }

    // ── Items catalogs ────────────────────────────────────────────────────
    public DbSet<Brand> Brands => Set<Brand>();

    // ── Auth ──────────────────────────────────────────────────────────────
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<IdentityUser> IdentityUsers => Set<IdentityUser>();
    public DbSet<CompanyUserMembership> CompanyUserMemberships => Set<CompanyUserMembership>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<CompanyUserBranch> CompanyUserBranches => Set<CompanyUserBranch>();
    public DbSet<CompanyUserPreferences> CompanyUserPreferences => Set<CompanyUserPreferences>();

    // ── Access / RBAC ─────────────────────────────────────────────────────
    public DbSet<AccessProfile> AccessProfiles => Set<AccessProfile>();
    public DbSet<AccessProfilePermission> AccessProfilePermissions =>
        Set<AccessProfilePermission>();
    public DbSet<SecurityAdminScopeAssignment> SecurityAdminScopeAssignments =>
        Set<SecurityAdminScopeAssignment>();

    // ── Tenants ───────────────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();

    // ── Geography ─────────────────────────────────────────────────────────
    public DbSet<GeoProvince> GeoProvinces => Set<GeoProvince>();
    public DbSet<GeoCanton> GeoCantons => Set<GeoCanton>();
    public DbSet<GeoParish> GeoParishes => Set<GeoParish>();

    // ── Branches / Warehouses ─────────────────────────────────────────────
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    // ── Audit ─────────────────────────────────────────────────────────────
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();

    // ── MasterData BC ─────────────────────────────────────────────────────
    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();
    public DbSet<BusinessPartnerRole> BusinessPartnerRoles => Set<BusinessPartnerRole>();
    public DbSet<CompanyBpTradingSettings> CompanyBpTradingSettings =>
        Set<CompanyBpTradingSettings>();
    public DbSet<BusinessPartnerLocation> BusinessPartnerLocations =>
        Set<BusinessPartnerLocation>();
    public DbSet<BusinessPartnerContact> BusinessPartnerContacts => Set<BusinessPartnerContact>();
    public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();
    public DbSet<LegalEntityTypeCatalog> LegalEntityTypeCatalog => Set<LegalEntityTypeCatalog>();

    // ── MasterData BC — catálogos de clasificación BusinessPartner (CLASS-BP-CATALOGS-01) ──
    public DbSet<CustomerCategory> CustomerCategories => Set<CustomerCategory>();
    public DbSet<CustomerSegment> CustomerSegments => Set<CustomerSegment>();
    public DbSet<CustomerCreditRating> CustomerCreditRatings => Set<CustomerCreditRating>();
    public DbSet<LoyaltyTier> LoyaltyTiers => Set<LoyaltyTier>();
    public DbSet<CustomerInvoiceFormat> CustomerInvoiceFormats => Set<CustomerInvoiceFormat>();
    public DbSet<CustomerClassification> CustomerClassifications => Set<CustomerClassification>();
    public DbSet<SupplierCategory> SupplierCategories => Set<SupplierCategory>();
    public DbSet<SupplierType> SupplierTypes => Set<SupplierType>();
    public DbSet<SupplierRisk> SupplierRisks => Set<SupplierRisk>();
    public DbSet<SupplierRating> SupplierRatings => Set<SupplierRating>();
    public DbSet<PrimaryGoodType> PrimaryGoodTypes => Set<PrimaryGoodType>();
    public DbSet<SupplierSegment> SupplierSegments => Set<SupplierSegment>();

    // ── Navigation / UI ───────────────────────────────────────────────────
    public DbSet<UiNavGroup> UiNavGroups => Set<UiNavGroup>();
    public DbSet<UiNavItem> UiNavItems => Set<UiNavItem>();
    public DbSet<TenantCustomMenu> TenantCustomMenus => Set<TenantCustomMenu>();
    public DbSet<AppFeature> AppFeatures => Set<AppFeature>();

    // ── Configuration ─────────────────────────────────────────────────────
    public DbSet<ConfigGlobal> ConfigGlobals => Set<ConfigGlobal>();
    public DbSet<ConfigModule> ConfigModules => Set<ConfigModule>();
    public DbSet<ConfigFeature> ConfigFeatures => Set<ConfigFeature>();
    public DbSet<OrgSetting> OrgSettings => Set<OrgSetting>();

    // ── SRI Catalogs (global reference data) ─────────────────────────────
    public DbSet<SriEmissionType> SriEmissionTypes => Set<SriEmissionType>();
    public DbSet<SriDocType> SriDocTypes => Set<SriDocType>();
    public DbSet<SriIdType> SriIdTypes => Set<SriIdType>();
    public DbSet<SriIdTypeUsage> SriIdTypeUsages => Set<SriIdTypeUsage>();
    public DbSet<SriVatRate> SriVatRates => Set<SriVatRate>();
    public DbSet<SriIceRate> SriIceRates => Set<SriIceRate>();
    public DbSet<SriRetentionCode> SriRetentionCodes => Set<SriRetentionCode>();
    public DbSet<SriPaymentMethod> SriPaymentMethods => Set<SriPaymentMethod>();
    public DbSet<SriTaxRegime> SriTaxRegimes => Set<SriTaxRegime>();
    public DbSet<SriTaxSupport> SriTaxSupports => Set<SriTaxSupport>();
    public DbSet<SriUom> SriUoms => Set<SriUom>();
    public DbSet<SriErrorCode> SriErrorCodes => Set<SriErrorCode>();
    public DbSet<SriCountry> SriCountries => Set<SriCountry>();
    public DbSet<SriSupplierType> SriSupplierTypes => Set<SriSupplierType>();

    // ── Company setup ─────────────────────────────────────────────────────
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Establishment> Establishments => Set<Establishment>();
    public DbSet<EmissionPoint> EmissionPoints => Set<EmissionPoint>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<GeneralParameter> GeneralParameters => Set<GeneralParameter>();

    // ── Items BC (canonical catalog) ──────────────────────────────────────
    public DbSet<ItemCategoryNode> ItemCategoryNodes => Set<ItemCategoryNode>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemVariant> ItemVariants => Set<ItemVariant>();
    public DbSet<ItemVariantAttribute> ItemVariantAttributes => Set<ItemVariantAttribute>();
    public DbSet<ItemVariantBarcode> ItemVariantBarcodes => Set<ItemVariantBarcode>();
    public DbSet<ItemImage> ItemImages => Set<ItemImage>();
    public DbSet<ItemUnitConversion> ItemUnitConversions => Set<ItemUnitConversion>();
    public DbSet<ItemSubstitute> ItemSubstitutes => Set<ItemSubstitute>();
    public DbSet<ItemPackagingLevel> ItemPackagingLevels => Set<ItemPackagingLevel>();
    public DbSet<AttributeGroup> AttributeGroups => Set<AttributeGroup>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<ItemTypeDefinition> ItemTypes => Set<ItemTypeDefinition>();
    public DbSet<BarcodeTypeDefinition> BarcodeTypes => Set<BarcodeTypeDefinition>();
    public DbSet<ItemMarginStatusDefinition> ItemMarginStatuses =>
        Set<ItemMarginStatusDefinition>();
    public DbSet<ItemAudit> ItemAudits => Set<ItemAudit>();

    // ── Pricing BC ────────────────────────────────────────────────────────
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<PricingRuleAudit> PricingRuleAudits => Set<PricingRuleAudit>();
    public DbSet<PriceListItemAudit> PriceListItemAudits => Set<PriceListItemAudit>();
    public DbSet<PriceListAudit> PriceListAudits => Set<PriceListAudit>();

    // ── Accounting BC (ADR-026 — fundamentos + Posting Engine, partida doble sin JournalFactory/JournalValidator reales) ─
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<JournalEntrySequence> JournalEntrySequences => Set<JournalEntrySequence>();
    public DbSet<PostingRule> PostingRules => Set<PostingRule>();
    public DbSet<PostingRuleLine> PostingRuleLines => Set<PostingRuleLine>();

    // ── ElectronicDocuments BC (núcleo — sin SOAP/SRI, ver Fases 1-7) ──────
    public DbSet<ElectronicDocument> ElectronicDocuments => Set<ElectronicDocument>();
    public DbSet<ElectronicDocumentAudit> ElectronicDocumentAudits =>
        Set<ElectronicDocumentAudit>();
    public DbSet<ElectronicDocumentSriMessage> ElectronicDocumentSriMessages =>
        Set<ElectronicDocumentSriMessage>();

    // ── Ride BC (walking skeleton — ver Fase 4 del plan de implementación, ADR-025) ────────
    public DbSet<RidePdfDocument> RidePdfDocuments => Set<RidePdfDocument>();

    // ── Purchases BC ───────────────────────────────────────────────────────
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceDetail> PurchaseInvoiceDetails => Set<PurchaseInvoiceDetail>();
    public DbSet<PurchasePaymentSchedule> PurchasePaymentSchedules =>
        Set<PurchasePaymentSchedule>();
    public DbSet<PurchaseInvoiceTaxSummary> PurchaseInvoiceTaxSummaries =>
        Set<PurchaseInvoiceTaxSummary>();
    public DbSet<PurchasePayable> PurchasePayables => Set<PurchasePayable>();
    public DbSet<PurchasePayableInstallment> PurchasePayableInstallments =>
        Set<PurchasePayableInstallment>();
    public DbSet<PurchaseCommunication> PurchaseCommunications => Set<PurchaseCommunication>();
    public DbSet<PurchaseInvoiceAudit> PurchaseInvoiceAudits => Set<PurchaseInvoiceAudit>();
    public DbSet<PurchaseLinePvpAudit> PurchaseLinePvpAudits => Set<PurchaseLinePvpAudit>();
    public DbSet<IssuedWithholdingAudit> IssuedWithholdingAudits => Set<IssuedWithholdingAudit>();
    public DbSet<IssuedWithholding> IssuedWithholdings => Set<IssuedWithholding>();
    public DbSet<IssuedWithholdingDetail> IssuedWithholdingDetails =>
        Set<IssuedWithholdingDetail>();
    public DbSet<PurchaseReceptionDocument> PurchaseReceptionDocuments =>
        Set<PurchaseReceptionDocument>();
    public DbSet<PurchaseReceptionLine> PurchaseReceptionLines => Set<PurchaseReceptionLine>();

    // ── P0-02: PurchaseReturn + SupplierCredit ──────────────────────────────
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnDetail> PurchaseReturnDetails => Set<PurchaseReturnDetail>();
    public DbSet<PurchaseReturnAudit> PurchaseReturnAudits => Set<PurchaseReturnAudit>();
    public DbSet<PurchaseReturnSequence> PurchaseReturnSequences => Set<PurchaseReturnSequence>();
    public DbSet<SupplierCredit> SupplierCredits => Set<SupplierCredit>();
    public DbSet<SupplierCreditMovement> SupplierCreditMovements => Set<SupplierCreditMovement>();
    public DbSet<SupplierCreditAudit> SupplierCreditAudits => Set<SupplierCreditAudit>();
    public DbSet<CompanyFinancialDestination> CompanyFinancialDestinations =>
        Set<CompanyFinancialDestination>();
    public DbSet<CompanyFinancialDestinationAudit> CompanyFinancialDestinationAudits =>
        Set<CompanyFinancialDestinationAudit>();
    public DbSet<SupplierCreditRefundTransaction> SupplierCreditRefundTransactions =>
        Set<SupplierCreditRefundTransaction>();

    // ── FLOW-READY-02C: PurchaseCreditNote (descuento/promoción) ────────────
    public DbSet<PurchaseCreditNote> PurchaseCreditNotes => Set<PurchaseCreditNote>();
    public DbSet<PurchaseCreditNoteDetail> PurchaseCreditNoteDetails =>
        Set<PurchaseCreditNoteDetail>();
    public DbSet<PurchaseCreditNoteTaxSummary> PurchaseCreditNoteTaxSummaries =>
        Set<PurchaseCreditNoteTaxSummary>();

    // ── Sales BC ──────────────────────────────────────────────────────────
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceDetail> SalesInvoiceDetails => Set<SalesInvoiceDetail>();
    public DbSet<SalesInvoicePayment> SalesInvoicePayments => Set<SalesInvoicePayment>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<SalesReceivable> SalesReceivables => Set<SalesReceivable>();
    public DbSet<SalesReceivableInstallment> SalesReceivableInstallments =>
        Set<SalesReceivableInstallment>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnDetail> SalesReturnDetails => Set<SalesReturnDetail>();
    public DbSet<SalesReturnRefundAllocation> SalesReturnRefundAllocations =>
        Set<SalesReturnRefundAllocation>();
    public DbSet<SalesReturnAudit> SalesReturnAudits => Set<SalesReturnAudit>();

    // ── Finance BC ─────────────────────────────────────────────────────────
    public DbSet<CreditTerm> CreditTerms => Set<CreditTerm>();
    public DbSet<CreditInstallment> CreditInstallments => Set<CreditInstallment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentApplicationLine> PaymentApplicationLines => Set<PaymentApplicationLine>();

    // ── Inventory (lots / serials / stock) ───────────────────────────────
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<SerialNumber> SerialNumbers => Set<SerialNumber>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<CurrentStock> CurrentStocks => Set<CurrentStock>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockAdjustmentLine> StockAdjustmentLines => Set<StockAdjustmentLine>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();

    // ── Media ──────────────────────────────────────────────────────────────
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    // ── Configuration ─────────────────────────────────────────────────────
    public DbSet<SriSettings> SriSettings => Set<SriSettings>();

    // ── Setup (first-run gate) ────────────────────────────────────────────
    public DbSet<SystemSetupState> SystemSetupStates => Set<SystemSetupState>();

    // ── Outbox (event-driven foundation) ─────────────────────────────────
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // ── Caja (Cash Management) ──────────────────────────────────────────
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<CashClosingCount> CashClosingCounts => Set<CashClosingCount>();

    private Guid CurrentTenantId => FilterTenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);
        EnterpriseQueryFilterConfigurator.Apply(modelBuilder, this);
        // Navigation seeding moved to NavigationSyncService (startup upsert).
        // Historic migrations with seed data remain valid — they won't be regenerated.
        base.OnModelCreating(modelBuilder);
    }
}
