using System.Linq;
using ERP.Domain.Modules.Logistics.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Common;
using ERP.Domain.Products.Entities;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Security.Entities;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Geography.Entities;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Subscriptions.Entities;
using ERP.Domain.Modules.Menu.Entities;
using ERP.Domain.Navigation.Entities;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Cash.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Auxiliary.Entities;
using ERP.Application.Common;
using System.Linq.Expressions;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// DbContext centralizado para todas las entidades (31 + 32 nuevos DbSets).
/// 
/// ⚠️ RIESGO ARQUITECTÓNICO: Este contexto está concentrando demasiados módulos.
/// 
/// ESTADO ACTUAL (Permitido para monolito modular):
/// - Contabilidad: Account, JournalEntry, JournalEntryLine
/// - Productos: Product, ProductLine, ProductCategory, ProductSubcategory, Brand, ProductType, TaxRate, UnitOfMeasure, Tariff
/// - Autenticación: User, IdentityUser, Membership
/// - Tenants: Tenant
/// - Seguridad: AccessProfile, AccessProfilePermission, SecurityAdminScopeAssignment
/// - Geografía: SriCountry (países), GeoProvince, GeoCanton, GeoParish
/// - Auditoría: UserActivity
/// - Ventas: Customer
/// - SaaS: SaasFeatureDefinition, SaasPlan, SaasPlanFeature, TenantSaasSubscription, TenantSubscriptionFeatureOverride, TenantSubscriptionUsage
/// - UI/Config: UiNavGroup, UiNavItem, ConfigGlobal, ConfigModule, ConfigFeature
/// - Sucursales: Branch
/// 
/// PRÓXIMOS PASOS RECOMENDADOS:
/// 1. Separar configuraciones de EF Core por módulo (ver Configurations/ folder)
///    - Usar IEntityTypeConfiguration<T> en carpetas específicas
///    - Mantener ApplyConfigurationsFromAssembly() automatizado
/// 2. Evaluar separación a múltiples DbContext por módulo mayor
///    - No necesariamente crear DbContext separados HOY
///    - Pero sí preparar el código para una futura separación
/// 3. Usar convenciones estrictas en repositorios
///    - Cada módulo: IXyzRepository interfaz + XyzRepository implementación
///    - Todos los repositorios inyectan solo ErpDbContext
/// 4. Considerar Command/Query segregation (CQRS) para reportes
/// 
/// MULTI-TENANCY:
/// - Filtro automático por tenant en QueryFilter para todas las ITenantEntity
/// - CurrentTenantId retorna Guid.Empty si no autenticado (seguro por defecto)
/// - Todas las queries se filtran automáticamente, no se olvidan excepciones
/// </summary>
public class ErpDbContext : DbContext
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IPublisher _publisher;
    private readonly IPlatformQueryAccessor _platform;

    public ErpDbContext(
        DbContextOptions<ErpDbContext> options,
        ICurrentTenant currentTenant,
        IPublisher publisher,
        IPlatformQueryAccessor platform) : base(options)
    {
        _currentTenant = currentTenant;
        _publisher     = publisher;
        _platform      = platform;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None)
            .GetAwaiter().GetResult();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await SyncTenantSubscriptionsFromPlanCodeAsync(cancellationToken);

        var entitiesWithEvents = ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
        foreach (var entity in entitiesWithEvents)
            entity.ClearDomainEvents();

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        foreach (var @event in domainEvents)
            await _publisher.Publish((INotification)@event, cancellationToken);

        if (ChangeTracker.HasChanges())
            result += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        return result;
    }

    private async Task SyncTenantSubscriptionsFromPlanCodeAsync(CancellationToken cancellationToken)
    {
        var tenantEntries = ChangeTracker.Entries<Tenant>()
            .Where(e =>
                e.State == EntityState.Added ||
                (e.State == EntityState.Modified && e.Property(nameof(Tenant.PlanCode)).IsModified))
            .ToList();

        if (tenantEntries.Count == 0)
            return;

        var planCodeSet = tenantEntries
            .Select(e => (e.Entity.PlanCode ?? string.Empty).Trim().ToLowerInvariant())
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var planByCode = planCodeSet.Count == 0
            ? new Dictionary<string, Guid>(StringComparer.Ordinal)
            : await SaasPlans.AsNoTracking()
                .Where(p => planCodeSet.Contains((p.Code ?? string.Empty).Trim().ToLower()))
                .ToDictionaryAsync(
                    p => (p.Code ?? string.Empty).Trim().ToLowerInvariant(),
                    p => p.Id,
                    StringComparer.Ordinal,
                    cancellationToken);

        foreach (var entry in tenantEntries)
        {
            var tenant = entry.Entity;
            var tenantId = tenant.Id;
            if (tenantId == Guid.Empty)
                continue;

            var normalizedPlanCode = (tenant.PlanCode ?? string.Empty).Trim().ToLowerInvariant();
            var planId = Guid.Empty;
            var hasValidPlan = normalizedPlanCode.Length > 0 && planByCode.TryGetValue(normalizedPlanCode, out planId);

            var existing = await _platform
                .Unfiltered(TenantSaasSubscriptions, PlatformQueryReason.DbContextSync)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

            if (!hasValidPlan)
            {
                if (existing is not null && existing.Status == TenantSubscriptionStatus.Active)
                    existing.Cancel(Guid.Empty);
                continue;
            }

            if (existing is null)
            {
                await TenantSaasSubscriptions.AddAsync(
                    TenantSaasSubscription.Create(tenantId, planId, Guid.Empty),
                    cancellationToken);
                continue;
            }

            if (existing.PlanId == planId && existing.Status == TenantSubscriptionStatus.Active)
                continue;

            existing.ReassignPlan(planId, Guid.Empty);
        }
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<AccountingSetup> AccountingSetups => Set<AccountingSetup>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<ProductLine> ProductLines => Set<ProductLine>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductSubcategory> ProductSubcategories => Set<ProductSubcategory>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<User>         Users         => Set<User>();
    public DbSet<FirstRunSetupState> FirstRunSetupStates => Set<FirstRunSetupState>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<IdentityUser> IdentityUsers => Set<IdentityUser>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<AccessProfile> AccessProfiles => Set<AccessProfile>();
    public DbSet<AccessProfilePermission> AccessProfilePermissions => Set<AccessProfilePermission>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SecurityAdminScopeAssignment> SecurityAdminScopeAssignments => Set<SecurityAdminScopeAssignment>();
    public DbSet<GeoProvince> GeoProvinces => Set<GeoProvince>();
    public DbSet<GeoCanton> GeoCantons => Set<GeoCanton>();
    public DbSet<GeoParish> GeoParishes => Set<GeoParish>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<Customer> Customers => Set<Customer>();
    // ── Sales (traditional) ───────────────────────────────────────────────
    public DbSet<SalesBill>           SalesBills          => Set<SalesBill>();
    public DbSet<SalesBillLine>       SalesBillLines      => Set<SalesBillLine>();
    // ── Sales (unified schema) ────────────────────────────────────────────
    public DbSet<SalesDocument>       SalesDocuments      => Set<SalesDocument>();
    public DbSet<SalesDetail>         SalesDetails        => Set<SalesDetail>();
    public DbSet<SalesPayment>        SalesPayments       => Set<SalesPayment>();
    public DbSet<SalesElectronicDoc>  SalesElectronicDocs => Set<SalesElectronicDoc>();
    public DbSet<SalesNote>           SalesNotes          => Set<SalesNote>();
    public DbSet<SalesNoteLine>       SalesNoteLines      => Set<SalesNoteLine>();
    public DbSet<SalesRetention>       SalesRetentions       => Set<SalesRetention>();
    public DbSet<SalesRetentionLine>   SalesRetentionLines   => Set<SalesRetentionLine>();
    public DbSet<SalesWithholding>     SalesWithholdings     => Set<SalesWithholding>();
    public DbSet<SalesWithholdingLine> SalesWithholdingLines => Set<SalesWithholdingLine>();

    // ── Configuration ─────────────────────────────────────────────────────
    public DbSet<SriSettings>       SriSettings       => Set<SriSettings>();
    public DbSet<RetentionSettings> RetentionSettings => Set<RetentionSettings>();
    public DbSet<BillingSettings>   BillingSettings   => Set<BillingSettings>();

    // ── Logistics / Warehouses ────────────────────────────────────────────
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    // ── Suppliers ────────────────────────────────────────────────────────
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    // ── Purchasing (traditional) ──────────────────────────────────────────
    public DbSet<PurchBill>           PurchBills            => Set<PurchBill>();
    public DbSet<PurchBillLine>       PurchBillLines        => Set<PurchBillLine>();
    public DbSet<PurchNote>           PurchNotes            => Set<PurchNote>();
    public DbSet<PurchNoteLine>       PurchNoteLines        => Set<PurchNoteLine>();
    public DbSet<IssuedRetention>     IssuedRetentions      => Set<IssuedRetention>();
    public DbSet<PurchRetentionLine>  PurchRetentionLines   => Set<PurchRetentionLine>();
    public DbSet<PurchWarehouseAlloc> PurchWarehouseAllocs  => Set<PurchWarehouseAlloc>();
    public DbSet<PurchaseOrder>       PurchaseOrders        => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine>   PurchaseOrderLines    => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderBill>   PurchaseOrderBills    => Set<PurchaseOrderBill>();

    // ── Expenses ──────────────────────────────────────────────────────────
    public DbSet<ExpenseInvoice> ExpenseInvoices => Set<ExpenseInvoice>();
    public DbSet<ExpenseDetail>  ExpenseDetails  => Set<ExpenseDetail>();
    public DbSet<ExpenseDocument> ExpenseDocuments => Set<ExpenseDocument>();

    // ── Purchasing (unified schema) ───────────────────────────────────────
    public DbSet<PurchaseDocument>       PurchaseDocuments       => Set<PurchaseDocument>();
    public DbSet<PurchaseDetail>         PurchaseDetails         => Set<PurchaseDetail>();
    public DbSet<PurchaseWithholding>    PurchaseWithholdings    => Set<PurchaseWithholding>();
    public DbSet<PurchaseWithholdingLine> PurchaseWithholdingLines => Set<PurchaseWithholdingLine>();

    // ── Cash / Banking ────────────────────────────────────────────────────
    public DbSet<BankAccount>      BankAccounts      => Set<BankAccount>();
    public DbSet<BankStatement>    BankStatements    => Set<BankStatement>();
    public DbSet<BankTransaction>  BankTransactions  => Set<BankTransaction>();
    public DbSet<PettyCash>        PettyCashes       => Set<PettyCash>();
    public DbSet<CashCount>        CashCounts        => Set<CashCount>();
    public DbSet<PettyCashExpense> PettyCashExpenses => Set<PettyCashExpense>();

    // ── Inventory ─────────────────────────────────────────────────────────
    public DbSet<CurrentStock>        CurrentStocks        => Set<CurrentStock>();
    public DbSet<StockMovement>       StockMovements       => Set<StockMovement>();
    public DbSet<KardexSnapshot>      KardexSnapshots      => Set<KardexSnapshot>();
    public DbSet<KardexReport>        KardexReports        => Set<KardexReport>();
    public DbSet<StockTransfer>       StockTransfers       => Set<StockTransfer>();
    public DbSet<StockTransferLine>   StockTransferLines   => Set<StockTransferLine>();
    public DbSet<StockAdjustment>     StockAdjustments     => Set<StockAdjustment>();
    public DbSet<StockAdjustmentLine> StockAdjustmentLines => Set<StockAdjustmentLine>();

    public DbSet<SaasFeatureDefinition> SaasFeatureDefinitions => Set<SaasFeatureDefinition>();
    public DbSet<SaasPlan> SaasPlans => Set<SaasPlan>();
    public DbSet<SaasPlanFeature> SaasPlanFeatures => Set<SaasPlanFeature>();
    public DbSet<TenantSaasSubscription> TenantSaasSubscriptions => Set<TenantSaasSubscription>();
    public DbSet<TenantSubscriptionFeatureOverride> TenantSubscriptionFeatureOverrides => Set<TenantSubscriptionFeatureOverride>();
    public DbSet<TenantSubscriptionUsage> TenantSubscriptionUsages => Set<TenantSubscriptionUsage>();

    public DbSet<UiNavGroup> UiNavGroups => Set<UiNavGroup>();
    public DbSet<UiNavItem> UiNavItems => Set<UiNavItem>();
    public DbSet<TenantCustomMenu> TenantCustomMenus => Set<TenantCustomMenu>();
    public DbSet<AppFeature> AppFeatures => Set<AppFeature>();
    public DbSet<ConfigGlobal> ConfigGlobals => Set<ConfigGlobal>();
    public DbSet<ConfigModule> ConfigModules => Set<ConfigModule>();
    public DbSet<ConfigFeature> ConfigFeatures => Set<ConfigFeature>();

    // ── Catálogos SRI (globales, sin tenant_id) ───────────────────────────
    public DbSet<SriEnvironment>    SriEnvironments    => Set<SriEnvironment>();
    public DbSet<SriEmissionType>   SriEmissionTypes   => Set<SriEmissionType>();
    public DbSet<SriDocType>        SriDocTypes        => Set<SriDocType>();
    public DbSet<SriIdType>         SriIdTypes         => Set<SriIdType>();
    public DbSet<SriVatRate>        SriVatRates        => Set<SriVatRate>();
    public DbSet<SriIceRate>        SriIceRates        => Set<SriIceRate>();
    public DbSet<SriRetentionCode>  SriRetentionCodes  => Set<SriRetentionCode>();
    public DbSet<SriPaymentMethod>  SriPaymentMethods  => Set<SriPaymentMethod>();
    public DbSet<SriTaxRegime>      SriTaxRegimes      => Set<SriTaxRegime>();
    public DbSet<SriTaxSupport>     SriTaxSupports     => Set<SriTaxSupport>();
    public DbSet<SriUom>            SriUoms            => Set<SriUom>();
    public DbSet<SriErrorCode>      SriErrorCodes      => Set<SriErrorCode>();
    public DbSet<SriCountry>        SriCountries       => Set<SriCountry>();

    // ── Company / Configuración SRI por empresa ───────────────────────────
    public DbSet<Company>            Companies          => Set<Company>();
    public DbSet<DigitalCertificate> DigitalCerts       => Set<DigitalCertificate>();
    public DbSet<Establishment>      Establishments     => Set<Establishment>();
    public DbSet<EmissionPoint>      EmissionPoints     => Set<EmissionPoint>();
    public DbSet<DocumentSequence>   DocumentSequences  => Set<DocumentSequence>();
    public DbSet<GeneralParameter>   GeneralParameters  => Set<GeneralParameter>();

    // ── Documentos Electrónicos ───────────────────────────────────────────
    public DbSet<ElectronicDoc>       ElectronicDocs       => Set<ElectronicDoc>();
    public DbSet<DocPayment>          DocPayments          => Set<DocPayment>();
    public DbSet<DocTax>              DocTaxes             => Set<DocTax>();
    public DbSet<SalesInvoice>        SalesInvoices        => Set<SalesInvoice>();
    public DbSet<CreditNote>          CreditNotes          => Set<CreditNote>();
    public DbSet<DebitNote>           DebitNotes           => Set<DebitNote>();
    public DbSet<DeliveryGuide>       DeliveryGuides       => Set<DeliveryGuide>();
    public DbSet<WithholdingCertificate> WithholdingCertificates => Set<WithholdingCertificate>();
    public DbSet<PurchaseSettlement>  PurchaseSettlements  => Set<PurchaseSettlement>();
    public DbSet<InvoiceDetail>       InvoiceDetails       => Set<InvoiceDetail>();
    public DbSet<NoteDetail>          NoteDetails          => Set<NoteDetail>();
    public DbSet<DeliveryDetail>      DeliveryDetails      => Set<DeliveryDetail>();
    public DbSet<WithholdingDetail>   WithholdingDetails   => Set<WithholdingDetail>();

    // ── Documentos recibidos (compras / retenciones de clientes) ─────────
    public DbSet<PurchaseInvoice>    PurchaseInvoices     => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceDetail> PurchaseInvoiceDetails => Set<PurchaseInvoiceDetail>();
    public DbSet<SupplierNote>       SupplierNotes        => Set<SupplierNote>();
    public DbSet<SupplierNoteDetail> SupplierNoteDetails  => Set<SupplierNoteDetail>();
    public DbSet<ReceivedWithholding> ReceivedWithholdings => Set<ReceivedWithholding>();
    public DbSet<ReceivedWhDetail>   ReceivedWhDetails    => Set<ReceivedWhDetail>();

    // ── Auxiliares SRI (logs, reintentos, devolución IVA) ─────────────────
    public DbSet<WsLog>         WsLogs        => Set<WsLog>();
    public DbSet<RetryControl>  RetryControls => Set<RetryControl>();
    public DbSet<VatRefund>     VatRefunds    => Set<VatRefund>();

    /// <summary>
    /// Evaluada en cada query, no al compilar el modelo.
    /// Si el request no está autenticado, retorna Guid.Empty para que el filtro
    /// global no retorne filas (ya que TenantId nunca debe ser Guid.Empty en
    /// entidades multi-tenant). Esto permite endpoints anónimos como login/reset.
    /// </summary>
    private Guid CurrentTenantId
    {
        get
        {
            return _currentTenant.TenantId;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

        // Filtro global multi-tenant automático:
        // - Aplica a toda entidad NO-OWNED que implemente ITenantEntity.
        // - Evita que al agregar una nueva entidad se nos olvide registrar el filtro.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
                continue;

            var clrType = entityType.ClrType;
            if (clrType == typeof(Tenant))
                continue;
            if (!typeof(ITenantEntity).IsAssignableFrom(clrType))
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            var tenantProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var currentTenant = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
            var body = Expression.Equal(tenantProperty, currentTenant);
            var lambda = Expression.Lambda(body, parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }

        base.OnModelCreating(modelBuilder);
    }
}
