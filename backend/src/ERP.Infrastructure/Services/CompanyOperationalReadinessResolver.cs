using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Infrastructure.Services;

/// <summary>
/// COMPANY-OPERATING-SETUP-01: única implementación de <see cref="ICompanyOperationalReadinessResolver"/>.
/// Orquesta fuentes/resolvers existentes — nunca lee org_settings directamente, nunca persiste.
/// Ver el doc-comment de la interfaz para las reglas de CanSell/CanIssueElectronicInvoices/
/// CanUseInventory/CanUseCashRegister.
/// </summary>
public sealed class CompanyOperationalReadinessResolver : ICompanyOperationalReadinessResolver
{
    private readonly ICompanyRepository _companyRepo;
    private readonly IBranchRepository _branchRepo;
    private readonly IEstablishmentRepository _establishmentRepo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICashRegisterRepository _cashRegisterRepo;
    private readonly IPriceListRepository _priceListRepo;
    private readonly ISriSettingsRepository _sriRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IInvoiceDefaultsResolver _invoiceDefaultsResolver;
    private readonly ISalesFiscalPolicyResolver _salesFiscalPolicyResolver;
    private readonly ICompanyBrandingResolver _brandingResolver;
    private readonly ICatalogConfigurationResolver _catalogConfigResolver;

    public CompanyOperationalReadinessResolver(
        ICompanyRepository companyRepo,
        IBranchRepository branchRepo,
        IEstablishmentRepository establishmentRepo,
        IEmissionPointRepository emissionPointRepo,
        IWarehouseRepository warehouseRepo,
        ICashRegisterRepository cashRegisterRepo,
        IPriceListRepository priceListRepo,
        ISriSettingsRepository sriRepo,
        IItemRepository itemRepo,
        IInvoiceDefaultsResolver invoiceDefaultsResolver,
        ISalesFiscalPolicyResolver salesFiscalPolicyResolver,
        ICompanyBrandingResolver brandingResolver,
        ICatalogConfigurationResolver catalogConfigResolver
    )
    {
        _companyRepo = companyRepo;
        _branchRepo = branchRepo;
        _establishmentRepo = establishmentRepo;
        _emissionPointRepo = emissionPointRepo;
        _warehouseRepo = warehouseRepo;
        _cashRegisterRepo = cashRegisterRepo;
        _priceListRepo = priceListRepo;
        _sriRepo = sriRepo;
        _itemRepo = itemRepo;
        _invoiceDefaultsResolver = invoiceDefaultsResolver;
        _salesFiscalPolicyResolver = salesFiscalPolicyResolver;
        _brandingResolver = brandingResolver;
        _catalogConfigResolver = catalogConfigResolver;
    }

    public async Task<CompanyOperationalReadinessResult> GetAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    )
    {
        var company = await _companyRepo.GetByIdForTenantAsync(companyId, tenantId, cancellationToken);

        var branches = await _branchRepo.GetAsync(tenantId, activeFilter: true, cancellationToken: cancellationToken);
        var mainBranch = branches.FirstOrDefault(b => b.IsMainBranch);

        var mainEstablishment = await _establishmentRepo.GetMainByCompanyAsync(
            tenantId,
            companyId,
            cancellationToken
        );

        var defaultEmissionPoint = await _emissionPointRepo.GetDefaultForCompanyAsync(
            tenantId,
            companyId,
            cancellationToken
        );

        var mainWarehouse =
            mainBranch is not null
                ? await _warehouseRepo.GetMainForBranchAsync(tenantId, mainBranch.Id, cancellationToken)
                : null;

        var companyWarehouses = await _warehouseRepo.GetAsync(
            tenantId,
            activeFilter: true,
            search: null,
            branchId: null,
            cancellationToken: cancellationToken
        );
        var hasAnyActiveWarehouse = companyWarehouses.Count > 0;

        var companyCashRegisters = await _cashRegisterRepo.GetAllByCompanyAsync(
            tenantId,
            companyId,
            activeFilter: true,
            search: null,
            ct: cancellationToken
        );
        var hasAnyActiveCashRegister = companyCashRegisters.Count > 0;

        var defaultPriceList = await _priceListRepo.GetDefaultAsync(tenantId, cancellationToken);

        var sriSettings = await _sriRepo.GetByCompanyIdAsync(companyId, cancellationToken);

        var invoiceDefaults = await _invoiceDefaultsResolver.GetAsync(
            tenantId,
            companyId,
            mainBranch?.Id,
            cancellationToken
        );

        var fiscalPolicy = await _salesFiscalPolicyResolver.GetEffectivePolicyAsync(cancellationToken);
        var branding = await _brandingResolver.GetAsync(tenantId, companyId, cancellationToken);
        var maxCategoryDepth = await _catalogConfigResolver.ResolveMaxCategoryDepthAsync(
            tenantId,
            companyId,
            cancellationToken
        );
        _ = maxCategoryDepth; // resuelto solo para confirmar que el fallback no lanza — siempre Ready.

        var hasActiveItems = (await _itemRepo.GetAllActiveAsync(tenantId, cancellationToken)).Count > 0;

        var identity = BuildIdentitySection(company, branding);
        var structure = BuildStructureSection(mainBranch, mainEstablishment, defaultEmissionPoint, mainWarehouse, hasAnyActiveCashRegister);
        var electronicInvoicing = BuildElectronicInvoicingSection(sriSettings, defaultEmissionPoint);
        var sales = BuildSalesSection(invoiceDefaults, defaultPriceList);
        var inventory = BuildInventorySection(hasAnyActiveWarehouse, hasActiveItems);
        var cashRegister = BuildCashRegisterSection(hasAnyActiveCashRegister);
        var documents = BuildDocumentsSection(branding);
        _ = fiscalPolicy; // ya reflejado en sales.consumerFinalPolicy (siempre Ready — tiene fallback DomainDefault).

        var sections = new List<ReadinessSection>
        {
            identity,
            structure,
            electronicInvoicing,
            sales,
            inventory,
            cashRegister,
            documents,
        };

        var allItems = sections.SelectMany(s => s.Items).ToList();

        bool AreaSatisfied(ReadinessBlockingArea area) =>
            !allItems.Any(i =>
                i.BlockingArea == area && i.Severity == ReadinessSeverity.Blocking && i.Status != ReadinessStatus.Ready
            );

        var canSell = AreaSatisfied(ReadinessBlockingArea.Sales);
        var canIssueElectronicInvoices = AreaSatisfied(ReadinessBlockingArea.ElectronicInvoicing);
        var canUseInventory = hasAnyActiveWarehouse;
        var canUseCashRegister = hasAnyActiveCashRegister;

        var hasBlockingGap = allItems.Any(i =>
            i.Severity == ReadinessSeverity.Blocking && i.Status != ReadinessStatus.Ready
        );
        var hasWarningGap = allItems.Any(i => i.Status == ReadinessStatus.Warning);

        var overallStatus = hasBlockingGap
            ? ReadinessStatus.Missing
            : hasWarningGap
                ? ReadinessStatus.Warning
                : ReadinessStatus.Ready;

        return new CompanyOperationalReadinessResult(
            OverallStatus: overallStatus,
            CanSell: canSell,
            CanIssueElectronicInvoices: canIssueElectronicInvoices,
            CanUseInventory: canUseInventory,
            CanUseCashRegister: canUseCashRegister,
            Sections: sections
        );
    }

    // ── Identidad de empresa ─────────────────────────────────────────────────
    private static ReadinessSection BuildIdentitySection(
        CompanyEntity? company,
        CompanyBrandingSettings branding
    )
    {
        var items = new List<ReadinessItem>
        {
            Item(
                "identity.taxId",
                !string.IsNullOrWhiteSpace(company?.TaxIdentificationNumber),
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.CompanyProfile
            ),
            Item(
                "identity.legalName",
                !string.IsNullOrWhiteSpace(company?.LegalName),
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.CompanyProfile
            ),
            Item(
                "identity.tradeName",
                !string.IsNullOrWhiteSpace(company?.TradeName),
                ReadinessSeverity.Warning,
                null,
                ReadinessActionTarget.CompanyProfile
            ),
            Item(
                "identity.corporateEmail",
                !string.IsNullOrWhiteSpace(company?.CorporateEmail),
                ReadinessSeverity.Warning,
                null,
                ReadinessActionTarget.CompanyProfile
            ),
        };

        return Section("identity", items);
    }

    // ── Estructura operativa ─────────────────────────────────────────────────
    private static ReadinessSection BuildStructureSection(
        Domain.Branches.Entities.Branch? mainBranch,
        Domain.Modules.Company.Entities.Establishment? mainEstablishment,
        Domain.Modules.Company.Entities.EmissionPoint? defaultEmissionPoint,
        Domain.Modules.Inventory.Entities.Warehouse? mainWarehouse,
        bool hasAnyActiveCashRegister
    )
    {
        var items = new List<ReadinessItem>
        {
            Item(
                "structure.mainBranch",
                mainBranch is not null,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.Branches
            ),
            Item(
                "structure.mainEstablishment",
                mainEstablishment is not null,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.Establishments
            ),
            Item(
                "structure.defaultEmissionPoint",
                defaultEmissionPoint is not null,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.EmissionPoints
            ),
            Item(
                "structure.mainWarehouse",
                mainWarehouse is not null,
                ReadinessSeverity.Warning,
                ReadinessBlockingArea.Inventory,
                ReadinessActionTarget.Warehouses
            ),
            Item(
                "structure.mainCashRegister",
                hasAnyActiveCashRegister,
                ReadinessSeverity.Warning,
                ReadinessBlockingArea.CashRegister,
                ReadinessActionTarget.CashRegisters
            ),
        };

        return Section("structure", items);
    }

    // ── Facturación electrónica ──────────────────────────────────────────────
    private static ReadinessSection BuildElectronicInvoicingSection(
        Domain.Configuration.Entities.SriSettings? sriSettings,
        Domain.Modules.Company.Entities.EmissionPoint? defaultEmissionPoint
    )
    {
        var hasSriConfig = sriSettings is not null && !string.IsNullOrWhiteSpace(sriSettings.WsdlUrl);
        var hasCertificate = !string.IsNullOrWhiteSpace(sriSettings?.CertP12Path);

        var items = new List<ReadinessItem>
        {
            Item(
                "electronicInvoicing.sriConfig",
                hasSriConfig,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.ElectronicInvoicing,
                ReadinessActionTarget.ElectronicInvoicingSettings
            ),
            Item(
                "electronicInvoicing.certificate",
                hasCertificate,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.ElectronicInvoicing,
                ReadinessActionTarget.ElectronicInvoicingSettings
            ),
            Item(
                "electronicInvoicing.emissionPoint",
                defaultEmissionPoint is not null,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.ElectronicInvoicing,
                ReadinessActionTarget.EmissionPoints
            ),
            // No existe fuente real de verificación de conexión/secuenciales SRI en el dominio
            // actual — nunca se inventa ese estado. Siempre Warning/Info, nunca bloqueante.
            Item(
                "electronicInvoicing.connectionCheck",
                ready: false,
                ReadinessSeverity.Info,
                null,
                ReadinessActionTarget.ElectronicInvoicingSettings,
                forceWarningNotMissing: true
            ),
        };

        return Section("electronicInvoicing", items);
    }

    // ── Ventas ────────────────────────────────────────────────────────────────
    private static ReadinessSection BuildSalesSection(
        InvoiceDefaultsResult invoiceDefaults,
        Domain.Modules.Pricing.Entities.PriceList? defaultPriceList
    )
    {
        var warehouseResolved =
            invoiceDefaults.DefaultWarehouseId is not null || !invoiceDefaults.RequiresManualWarehouseSelection;

        var items = new List<ReadinessItem>
        {
            Item(
                "sales.defaultDocType",
                !string.IsNullOrWhiteSpace(invoiceDefaults.DefaultDocTypeCode),
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.CompanySalesSettings
            ),
            Item(
                "sales.defaultPaymentMethod",
                !string.IsNullOrWhiteSpace(invoiceDefaults.DefaultSriPaymentMethodCode),
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.CompanySalesSettings
            ),
            Item(
                "sales.defaultPaymentTerm",
                invoiceDefaults.DefaultPaymentTermId is not null,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.CompanySalesSettings
            ),
            Item(
                "sales.defaultPriceList",
                defaultPriceList is not null,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Sales,
                ReadinessActionTarget.PriceLists
            ),
            // Bodega resuelta por sucursal, o selección manual permitida (nunca bloqueante — ver
            // IInvoiceDefaultsResolver, RequiresManualWarehouseSelection siempre habilita continuar).
            Item(
                "sales.defaultWarehouse",
                warehouseResolved,
                ReadinessSeverity.Warning,
                null,
                ReadinessActionTarget.Branches
            ),
            // Siempre resuelve (FallbackStrategy DomainDefault/Fallback) — informativo, nunca bloqueante.
            Item(
                "sales.consumerFinalPolicy",
                true,
                ReadinessSeverity.Info,
                null,
                ReadinessActionTarget.CompanyFiscalSettings
            ),
        };

        return Section("sales", items);
    }

    // ── Inventario ────────────────────────────────────────────────────────────
    private static ReadinessSection BuildInventorySection(bool hasAnyActiveWarehouse, bool hasActiveItems)
    {
        var items = new List<ReadinessItem>
        {
            Item(
                "inventory.mainWarehouse",
                hasAnyActiveWarehouse,
                ReadinessSeverity.Blocking,
                ReadinessBlockingArea.Inventory,
                ReadinessActionTarget.Warehouses
            ),
            // Profundidad de categorías siempre resuelve con SystemDefault — informativo, nunca bloqueante.
            Item("inventory.categoryDepth", true, ReadinessSeverity.Info, null, ReadinessActionTarget.Items),
            Item(
                "inventory.activeItems",
                hasActiveItems,
                ReadinessSeverity.Warning,
                null,
                ReadinessActionTarget.Items
            ),
        };

        return Section("inventory", items);
    }

    // ── Caja ──────────────────────────────────────────────────────────────────
    private static ReadinessSection BuildCashRegisterSection(bool hasAnyActiveCashRegister)
    {
        var items = new List<ReadinessItem>
        {
            Item(
                "cashRegister.mainCashRegister",
                hasAnyActiveCashRegister,
                ReadinessSeverity.Warning,
                ReadinessBlockingArea.CashRegister,
                ReadinessActionTarget.CashRegisters
            ),
        };

        return Section("cashRegister", items);
    }

    // ── Documentos y presentación ────────────────────────────────────────────
    private static ReadinessSection BuildDocumentsSection(CompanyBrandingSettings branding)
    {
        var items = new List<ReadinessItem>
        {
            Item(
                "documents.branding",
                !string.IsNullOrWhiteSpace(branding.LogoStoragePath),
                ReadinessSeverity.Warning,
                null,
                ReadinessActionTarget.CompanyBranding
            ),
            Item(
                "documents.footerText",
                !string.IsNullOrWhiteSpace(branding.DocumentFooterText),
                ReadinessSeverity.Warning,
                null,
                ReadinessActionTarget.CompanyBranding
            ),
            // Siempre resuelve con SystemDefault — informativo, nunca bloqueante.
            Item("documents.presentationDecimals", true, ReadinessSeverity.Info, null, ReadinessActionTarget.DecimalSettings),
            // No hay verificación real de generación de RIDE sin emitir un documento — informativo.
            Item("documents.ride", true, ReadinessSeverity.Info, null, ReadinessActionTarget.ElectronicInvoicingSettings),
        };

        return Section("documents", items);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static ReadinessItem Item(
        string code,
        bool ready,
        ReadinessSeverity severity,
        ReadinessBlockingArea? blockingArea,
        ReadinessActionTarget? actionTarget,
        bool forceWarningNotMissing = false
    )
    {
        var status = ready
            ? ReadinessStatus.Ready
            : forceWarningNotMissing
                ? ReadinessStatus.Warning
                : ReadinessStatus.Missing;

        return new ReadinessItem(code, status, severity, blockingArea, actionTarget);
    }

    private static ReadinessSection Section(string code, IReadOnlyList<ReadinessItem> items)
    {
        var hasBlockingGap = items.Any(i =>
            i.Severity == ReadinessSeverity.Blocking && i.Status != ReadinessStatus.Ready
        );
        var hasWarningGap = items.Any(i => i.Status == ReadinessStatus.Warning);

        var status = hasBlockingGap
            ? ReadinessStatus.Missing
            : hasWarningGap
                ? ReadinessStatus.Warning
                : ReadinessStatus.Ready;

        return new ReadinessSection(code, status, items);
    }
}
