using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;
using Microsoft.Extensions.Logging;
using Area = ERP.Domain.Modules.Company.Interfaces.ReadinessBlockingArea;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using EmissionPointEntity = ERP.Domain.Modules.Company.Entities.EmissionPoint;
using RequirementCodes = ERP.Application.Modules.ElectronicDocuments.Readiness.ElectronicEmissionRequirementCodes;

namespace ERP.Infrastructure.Services;

/// <summary>
/// COMPANY-OPERATING-SETUP-01: única implementación de <see cref="ICompanyOperationalReadinessResolver"/>.
/// Orquesta fuentes/resolvers/validadores existentes — nunca lee org_settings directamente, nunca
/// persiste. Los requisitos de empresa se evalúan una vez; cajas y bodegas, solo las de la sucursal
/// activa, porque OpenCashSession, CreateSalesDraft, ajustes y compras operan sobre la sucursal del
/// request. Cada caja se diagnostica con su propio punto de emisión: los requisitos electrónicos los
/// decide exclusivamente <see cref="IElectronicEmissionRequirementsValidator"/> (una vez por punto) y
/// el modo contable <see cref="AccountingOperationalDecision"/>.
/// Matriz completa: docs/architecture/company-readiness.md.
/// </summary>
public sealed class CompanyOperationalReadinessResolver : ICompanyOperationalReadinessResolver
{
    private const string IdentityTaxId = OperationalReadinessCodes.IdentityTaxId;
    private const string IdentityLegalName = OperationalReadinessCodes.IdentityLegalName;
    private const string StructureMainEstablishment =
        OperationalReadinessCodes.StructureMainEstablishment;
    private const string CashRegisterWithEmissionPoint =
        OperationalReadinessCodes.CashRegisterWithEmissionPoint;
    private const string ElectronicActivation = OperationalReadinessCodes.ElectronicActivation;
    private const string ElectronicVerificationFailed =
        OperationalReadinessCodes.ElectronicVerificationFailed;

    private static readonly Area[] NoArea = [];

    /// <summary>
    /// Una caja activa de la sucursal con su punto de emisión (null si no tiene uno activo en la
    /// empresa) y el resultado del validador para ese punto (solo puntos electrónicos).
    /// </summary>
    private sealed record RegisterCheck(
        CashRegister Register,
        EmissionPointEntity? EmissionPoint,
        IReadOnlyList<ElectronicEmissionRequirementIssue> Issues,
        bool VerificationFailed
    )
    {
        public bool IsElectronic => EmissionPoint?.EmissionType == EmissionType.Electronic;

        public bool CanIssue =>
            IsElectronic
            && !VerificationFailed
            && Issues.All(i => i.Severity != ElectronicEmissionRequirementSeverity.Required);

        /// <summary>Precondiciones de OpenCashSession + CreateSalesDraft + gate de AuthorizeSales.</summary>
        public bool CanSell => EmissionPoint is not null && (!IsElectronic || CanIssue);

        public IEnumerable<string> RequiredCodes =>
            VerificationFailed
                ? [ElectronicVerificationFailed]
                : Issues
                    .Where(i => i.Severity == ElectronicEmissionRequirementSeverity.Required)
                    .Select(i => i.Code);

        public IEnumerable<string> WarningCodes =>
            Issues
                .Where(i => i.Severity == ElectronicEmissionRequirementSeverity.Warning)
                .Select(i => i.Code);
    }

    private readonly ICompanyRepository _companyRepo;
    private readonly IEstablishmentRepository _establishmentRepo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICashRegisterRepository _cashRegisterRepo;
    private readonly IPriceListRepository _priceListRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IInvoiceDefaultsResolver _invoiceDefaultsResolver;
    private readonly ISalesFiscalPolicyResolver _salesFiscalPolicyResolver;
    private readonly ICompanyBrandingResolver _brandingResolver;
    private readonly ICatalogConfigurationResolver _catalogConfigResolver;
    private readonly IElectronicEmissionRequirementsValidator _emissionRequirements;
    private readonly AccountingOperationalDecision _accountingDecision;
    private readonly ICompanyClock _companyClock;
    private readonly ILogger<CompanyOperationalReadinessResolver> _logger;

    public CompanyOperationalReadinessResolver(
        ICompanyRepository companyRepo,
        IEstablishmentRepository establishmentRepo,
        IEmissionPointRepository emissionPointRepo,
        IWarehouseRepository warehouseRepo,
        ICashRegisterRepository cashRegisterRepo,
        IPriceListRepository priceListRepo,
        IItemRepository itemRepo,
        IInvoiceDefaultsResolver invoiceDefaultsResolver,
        ISalesFiscalPolicyResolver salesFiscalPolicyResolver,
        ICompanyBrandingResolver brandingResolver,
        ICatalogConfigurationResolver catalogConfigResolver,
        IElectronicEmissionRequirementsValidator emissionRequirements,
        AccountingOperationalDecision accountingDecision,
        ICompanyClock companyClock,
        ILogger<CompanyOperationalReadinessResolver> logger
    )
    {
        _companyRepo = companyRepo;
        _establishmentRepo = establishmentRepo;
        _emissionPointRepo = emissionPointRepo;
        _warehouseRepo = warehouseRepo;
        _cashRegisterRepo = cashRegisterRepo;
        _priceListRepo = priceListRepo;
        _itemRepo = itemRepo;
        _invoiceDefaultsResolver = invoiceDefaultsResolver;
        _salesFiscalPolicyResolver = salesFiscalPolicyResolver;
        _brandingResolver = brandingResolver;
        _catalogConfigResolver = catalogConfigResolver;
        _emissionRequirements = emissionRequirements;
        _accountingDecision = accountingDecision;
        _companyClock = companyClock;
        _logger = logger;
    }

    public async Task<CompanyOperationalReadinessResult> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken cancellationToken = default
    )
    {
        var company = await _companyRepo.GetByIdForTenantAsync(
            companyId,
            tenantId,
            cancellationToken
        );

        var mainEstablishment = await _establishmentRepo.GetMainByCompanyAsync(
            tenantId,
            companyId,
            cancellationToken
        );

        // Sin sucursal activa no hay cajas ni bodegas que evaluar (fail-closed: nunca se elige otra).
        var hasActiveWarehouse =
            branchId != Guid.Empty
            && (
                await _warehouseRepo.GetAsync(
                    tenantId,
                    activeFilter: true,
                    search: null,
                    branchId: branchId,
                    cancellationToken: cancellationToken
                )
            ).Any(w => w.CompanyId == companyId && w.BranchId == branchId);

        var registerChecks = await CheckBranchCashRegistersAsync(
            tenantId,
            companyId,
            branchId,
            cancellationToken
        );

        var defaultPriceList = await _priceListRepo.GetDefaultAsync(tenantId, cancellationToken);

        var invoiceDefaults = await _invoiceDefaultsResolver.GetAsync(
            tenantId,
            companyId,
            branchId == Guid.Empty ? null : branchId,
            cancellationToken
        );

        var fiscalPolicy = await _salesFiscalPolicyResolver.GetEffectivePolicyAsync(
            cancellationToken
        );
        var branding = await _brandingResolver.GetAsync(tenantId, companyId, cancellationToken);
        var maxCategoryDepth = await _catalogConfigResolver.ResolveMaxCategoryDepthAsync(
            tenantId,
            companyId,
            cancellationToken
        );
        _ = maxCategoryDepth; // resuelto solo para confirmar que el fallback no lanza — siempre Ready.

        var hasActiveItems =
            (await _itemRepo.GetAllActiveAsync(tenantId, cancellationToken)).Count > 0;

        // Sin empresa válida en el scope no se consulta nada más con ese scope: fail-closed.
        var accountingConfigured =
            company is not null
            && await _accountingDecision.ResolveAsync(
                tenantId,
                companyId,
                await _companyClock.TodayAsync(companyId, tenantId, cancellationToken),
                cancellationToken
            ) != AccountingApplicability.NotConfigured;

        var electronicFindings = BuildElectronicFindings(registerChecks);
        _ = fiscalPolicy; // ya reflejado en sales.consumerFinalPolicy (siempre Ready — tiene fallback DomainDefault).

        var electronicActive = registerChecks.Any(c => c.IsElectronic);
        var sections = new List<ReadinessSection>
        {
            BuildIdentitySection(company, electronicFindings),
            BuildStructureSection(mainEstablishment, electronicFindings),
            BuildAccountingSection(accountingConfigured),
            BuildCashRegisterSection(registerChecks),
            BuildElectronicInvoicingSection(electronicActive, electronicFindings),
            BuildSalesSection(invoiceDefaults, defaultPriceList),
            BuildInventorySection(hasActiveWarehouse, hasActiveItems),
            BuildDocumentsSection(branding),
        };

        var allItems = sections.SelectMany(s => s.Items).ToList();

        bool AreaSatisfied(Area area) =>
            !allItems.Any(i =>
                i.BlockingAreas.Contains(area)
                && i.Status is ReadinessStatus.Missing or ReadinessStatus.Warning
            );

        return new CompanyOperationalReadinessResult(
            BranchId: branchId,
            OverallStatus: Aggregate(allItems),
            CanSell: AreaSatisfied(Area.Sales),
            CanIssueElectronicInvoices: electronicActive
                && AreaSatisfied(Area.ElectronicInvoicing),
            CanUseInventory: AreaSatisfied(Area.Inventory),
            CanUseCashRegister: AreaSatisfied(Area.CashRegister),
            Sections: sections,
            CashRegisters: registerChecks.Select(ToCashRegisterResult).ToList()
        );
    }

    /// <summary>
    /// Cajas activas de la empresa en la sucursal activa (OpenCashSession exige
    /// <c>register.BranchId == sucursal activa</c>), cada una con su punto si existe y está activo
    /// en la empresa (CreateSalesDraft). Cada punto electrónico distinto se valida una sola vez.
    /// </summary>
    private async Task<IReadOnlyList<RegisterCheck>> CheckBranchCashRegistersAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken ct
    )
    {
        if (branchId == Guid.Empty)
            return [];

        var registers = (
            await _cashRegisterRepo.GetAllByCompanyAsync(
                tenantId,
                companyId,
                activeFilter: true,
                search: null,
                ct: ct
            )
        )
            .Where(r => r.CompanyId == companyId && r.BranchId == branchId)
            .ToList();

        var emissionPoints = new Dictionary<Guid, EmissionPointEntity?>();
        var validations =
            new Dictionary<Guid, (IReadOnlyList<ElectronicEmissionRequirementIssue> Issues, bool Failed)>();
        var checks = new List<RegisterCheck>(registers.Count);
        foreach (var register in registers)
        {
            EmissionPointEntity? emissionPoint = null;
            if (register.EmissionPointId is { } emissionPointId)
            {
                if (!emissionPoints.TryGetValue(emissionPointId, out emissionPoint))
                {
                    emissionPoint = await _emissionPointRepo.GetByIdForCompanyAsync(
                        tenantId,
                        companyId,
                        emissionPointId,
                        ct
                    );
                    emissionPoints[emissionPointId] = emissionPoint is { IsActive: true }
                        ? emissionPoint
                        : null;
                    emissionPoint = emissionPoints[emissionPointId];
                }
            }

            if (emissionPoint?.EmissionType != EmissionType.Electronic)
            {
                checks.Add(new(register, emissionPoint, [], false));
                continue;
            }

            if (!validations.TryGetValue(emissionPoint.Id, out var validation))
            {
                validation = await ValidateEmissionPointAsync(
                    tenantId,
                    companyId,
                    emissionPoint.Id,
                    ct
                );
                validations[emissionPoint.Id] = validation;
            }
            checks.Add(new(register, emissionPoint, validation.Issues, validation.Failed));
        }

        return checks;
    }

    /// <summary>Gate propietario para el tipo 01 (la capacidad diagnosticada). Un fallo técnico nunca es Ready.</summary>
    private async Task<(
        IReadOnlyList<ElectronicEmissionRequirementIssue> Issues,
        bool Failed
    )> ValidateEmissionPointAsync(
        Guid tenantId,
        Guid companyId,
        Guid emissionPointId,
        CancellationToken ct
    )
    {
        try
        {
            var result = await _emissionRequirements.ValidateAsync(
                new ElectronicEmissionRequirementsRequest(
                    tenantId,
                    companyId,
                    emissionPointId,
                    SriDocumentTypeCodes.Invoice
                ),
                ct
            );
            return (result.Issues, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Electronic emission requirements could not be verified for company {CompanyId}, emission point {EmissionPointId}",
                companyId,
                emissionPointId
            );
            return ([], true);
        }
    }

    /// <summary>
    /// Un ítem por código de requisito electrónico. Obligatorio incumplido en cualquier caja ⇒
    /// Missing (un punto correcto nunca oculta otro); solo condiciona una capacidad si la deja sin
    /// camino en la sucursal: ElectronicInvoicing si ninguna caja electrónica puede emitir, Sales si
    /// ninguna caja puede vender.
    /// </summary>
    private static IReadOnlyDictionary<string, ReadinessItem> BuildElectronicFindings(
        IReadOnlyList<RegisterCheck> checks
    )
    {
        var targets = checks
            .SelectMany(c => c.Issues)
            .GroupBy(i => i.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().ActionTarget, StringComparer.Ordinal);
        targets.TryAdd(
            ElectronicVerificationFailed,
            ReadinessActionTarget.ElectronicInvoicingSettings
        );

        var requiredAreas = new List<Area>();
        if (!checks.Any(c => c.CanSell))
            requiredAreas.Add(Area.Sales);
        if (!checks.Any(c => c.CanIssue))
            requiredAreas.Add(Area.ElectronicInvoicing);

        var findings = new Dictionary<string, ReadinessItem>(StringComparer.Ordinal);
        foreach (var code in checks.SelectMany(c => c.RequiredCodes).Distinct(StringComparer.Ordinal))
            findings[code] = new(
                code,
                ReadinessStatus.Missing,
                ReadinessSeverity.Blocking,
                requiredAreas,
                targets[code]
            );
        foreach (var code in checks.SelectMany(c => c.WarningCodes).Distinct(StringComparer.Ordinal))
            findings.TryAdd(
                code,
                new(code, ReadinessStatus.Warning, ReadinessSeverity.Warning, NoArea, targets[code])
            );

        return findings;
    }

    private static ReadinessCashRegister ToCashRegisterResult(RegisterCheck check)
    {
        var emissionPoint = check.EmissionPoint;
        var codes = emissionPoint is null
            ? [CashRegisterWithEmissionPoint]
            : check
                .RequiredCodes.Concat(check.WarningCodes)
                .Select(OperationalReadinessCodes.ForRequirement)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        // F-1..F-5: un punto físico es NotVerified (sin política física propietaria).
        var fiscalStatus = OperationalReadinessCodes.FiscalStatusFor(
            emissionPoint?.EmissionType,
            check.CanIssue
        );

        return new ReadinessCashRegister(
            check.Register.Id,
            check.Register.Code,
            check.Register.Name,
            emissionPoint?.Id,
            emissionPoint?.Establishment?.Code,
            emissionPoint?.Code,
            emissionPoint?.EmissionType,
            check.CanSell ? ReadinessStatus.Ready : ReadinessStatus.Missing,
            fiscalStatus,
            codes
        );
    }

    // ── Identidad de empresa ─────────────────────────────────────────────────
    // RUC y razón social solo los exige la emisión electrónica (validador); para el resto de
    // operaciones son datos recomendables del perfil.
    private static ReadinessSection BuildIdentitySection(
        CompanyEntity? company,
        IReadOnlyDictionary<string, ReadinessItem> electronicFindings
    )
    {
        // Un RUC provisional del bootstrap (IsTemporaryTaxIdentification) es un valor artificial,
        // no un RUC configurado.
        var hasOfficialTaxId =
            !string.IsNullOrWhiteSpace(company?.TaxIdentificationNumber)
            && !company.IsTemporaryTaxIdentification;

        var items = new List<ReadinessItem>
        {
            BaseItem(
                IdentityTaxId,
                hasOfficialTaxId,
                ReadinessActionTarget.CompanyProfile,
                electronicFindings
            ),
            BaseItem(
                IdentityLegalName,
                !string.IsNullOrWhiteSpace(company?.LegalName),
                ReadinessActionTarget.CompanyProfile,
                electronicFindings
            ),
            Item(
                "identity.tradeName",
                !string.IsNullOrWhiteSpace(company?.TradeName),
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.CompanyProfile
            ),
            Item(
                "identity.corporateEmail",
                !string.IsNullOrWhiteSpace(company?.CorporateEmail),
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.CompanyProfile
            ),
        };

        return Section("identity", items);
    }

    // ── Estructura operativa ─────────────────────────────────────────────────
    // El establecimiento matriz aporta dirMatriz a la emisión electrónica; ninguna venta física lo exige.
    private static ReadinessSection BuildStructureSection(
        Domain.Modules.Company.Entities.Establishment? mainEstablishment,
        IReadOnlyDictionary<string, ReadinessItem> electronicFindings
    ) =>
        Section(
            "structure",
            [
                BaseItem(
                    StructureMainEstablishment,
                    mainEstablishment is not null,
                    ReadinessActionTarget.Establishments,
                    electronicFindings
                ),
            ]
        );

    // ── Contabilidad ──────────────────────────────────────────────────────────
    // AccountingOperationalDecision.NotConfigured: AccountingPostingRecorder lanza dentro de la
    // transacción de la operación (handlers in-process de ErpDbContext.SaveChangesAsync), así que se
    // rechaza toda operación que contabiliza: autorizar ventas (y con ello emitir facturas),
    // confirmar compras y ajustes de stock, movimientos/depósitos de caja y cierres con diferencia.
    private static ReadinessSection BuildAccountingSection(bool accountingConfigured) =>
        Section(
            "accounting",
            [
                Item(
                    "accounting.operationalMode",
                    accountingConfigured,
                    ReadinessSeverity.Blocking,
                    [Area.Sales, Area.ElectronicInvoicing, Area.Inventory, Area.CashRegister],
                    ReadinessActionTarget.AccountingSettings
                ),
            ]
        );

    // ── Caja ──────────────────────────────────────────────────────────────────
    // CreateSalesDraft exige una caja abierta y OpenCashSession exige un punto de emisión en la
    // caja. Una caja de la sucursal sin punto activo deja el requisito pendiente aunque otra caja
    // esté lista; solo condiciona Sales/CashRegister si ninguna caja de la sucursal tiene punto.
    private static ReadinessSection BuildCashRegisterSection(IReadOnlyList<RegisterCheck> checks)
    {
        var allHavePoint = checks.Count > 0 && checks.All(c => c.EmissionPoint is not null);
        var anyHasPoint = checks.Any(c => c.EmissionPoint is not null);

        return Section(
            "cashRegister",
            [
                new(
                    CashRegisterWithEmissionPoint,
                    allHavePoint ? ReadinessStatus.Ready : ReadinessStatus.Missing,
                    ReadinessSeverity.Blocking,
                    anyHasPoint ? NoArea : [Area.Sales, Area.CashRegister],
                    ReadinessActionTarget.CashRegisters
                ),
            ]
        );
    }

    // ── Facturación electrónica ──────────────────────────────────────────────
    private static ReadinessSection BuildElectronicInvoicingSection(
        bool electronicActive,
        IReadOnlyDictionary<string, ReadinessItem> electronicFindings
    )
    {
        // Activa si alguna caja de la sucursal tiene un punto de emisión electrónico; si no,
        // NotApplicable (nunca un bloqueo).
        var items = new List<ReadinessItem>
        {
            new(
                ElectronicActivation,
                electronicActive ? ReadinessStatus.Ready : ReadinessStatus.NotApplicable,
                ReadinessSeverity.Info,
                NoArea,
                ReadinessActionTarget.EmissionPoints
            ),
        };
        items.AddRange(
            electronicFindings.Values.Where(f => !OperationalReadinessCodes.IsOwnedByBaseItem(f.Code))
        );

        return Section("electronicInvoicing", items);
    }

    // ── Ventas ────────────────────────────────────────────────────────────────
    // Defaults de captura: recomendables, ninguno bloquea la venta (precio → Item.BaseSalePrice,
    // tipo de documento → 01, condición de pago → Contado del sistema; ver company-readiness.md).
    private static ReadinessSection BuildSalesSection(
        InvoiceDefaultsResult invoiceDefaults,
        Domain.Modules.Pricing.Entities.PriceList? defaultPriceList
    )
    {
        var warehouseResolved =
            invoiceDefaults.DefaultWarehouseId is not null
            || !invoiceDefaults.RequiresManualWarehouseSelection;

        var items = new List<ReadinessItem>
        {
            Item(
                "sales.defaultDocType",
                !string.IsNullOrWhiteSpace(invoiceDefaults.DefaultDocTypeCode),
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.CompanySalesSettings
            ),
            Item(
                "sales.defaultPaymentMethod",
                !string.IsNullOrWhiteSpace(invoiceDefaults.DefaultSriPaymentMethodCode),
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.CompanySalesSettings
            ),
            Item(
                "sales.defaultPaymentTerm",
                invoiceDefaults.DefaultPaymentTermId is not null,
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.CompanySalesSettings
            ),
            Item(
                "sales.defaultPriceList",
                defaultPriceList is not null,
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.PriceLists
            ),
            // Bodega resuelta para la sucursal activa (BranchSetting → Warehouse.IsMain), o
            // selección manual permitida (RequiresManualWarehouseSelection siempre habilita continuar).
            Item(
                "sales.defaultWarehouse",
                warehouseResolved,
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.Branches
            ),
            // Siempre resuelve (FallbackStrategy DomainDefault/Fallback) — informativo, nunca bloqueante.
            Item(
                "sales.consumerFinalPolicy",
                true,
                ReadinessSeverity.Info,
                NoArea,
                ReadinessActionTarget.CompanyFiscalSettings
            ),
        };

        return Section("sales", items);
    }

    // ── Inventario ────────────────────────────────────────────────────────────
    // Ajustes y compras exigen una bodega de la sucursal activa.
    private static ReadinessSection BuildInventorySection(
        bool hasActiveWarehouse,
        bool hasActiveItems
    )
    {
        var items = new List<ReadinessItem>
        {
            Item(
                "inventory.activeWarehouse",
                hasActiveWarehouse,
                ReadinessSeverity.Blocking,
                [Area.Inventory],
                ReadinessActionTarget.Warehouses
            ),
            // Profundidad de categorías siempre resuelve con SystemDefault — informativo, nunca bloqueante.
            Item(
                "inventory.categoryDepth",
                true,
                ReadinessSeverity.Info,
                NoArea,
                ReadinessActionTarget.Items
            ),
            Item(
                "inventory.activeItems",
                hasActiveItems,
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.Items
            ),
        };

        return Section("inventory", items);
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
                NoArea,
                ReadinessActionTarget.CompanyBranding
            ),
            Item(
                "documents.footerText",
                !string.IsNullOrWhiteSpace(branding.DocumentFooterText),
                ReadinessSeverity.Warning,
                NoArea,
                ReadinessActionTarget.CompanyBranding
            ),
            // Siempre resuelve con SystemDefault — informativo, nunca bloqueante.
            Item(
                "documents.decimalPrecision",
                true,
                ReadinessSeverity.Info,
                NoArea,
                ReadinessActionTarget.DecimalSettings
            ),
        };

        return Section("documents", items);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static ReadinessItem Item(
        string code,
        bool ready,
        ReadinessSeverity severity,
        IReadOnlyList<Area> blockingAreas,
        ReadinessActionTarget? actionTarget
    )
    {
        ReadinessStatus status;
        if (ready)
            status = ReadinessStatus.Ready;
        else
            status = severity == ReadinessSeverity.Blocking
                ? ReadinessStatus.Missing
                : ReadinessStatus.Warning;

        return new ReadinessItem(code, status, severity, blockingAreas, actionTarget);
    }

    /// <summary>
    /// Ítem de un dato recomendable que además revisa el validador electrónico: si el validador lo
    /// rechazó en alguna caja, adopta ese veredicto (estado, severidad y capacidades); si no,
    /// Warning cuando falta.
    /// </summary>
    private static ReadinessItem BaseItem(
        string code,
        bool ready,
        ReadinessActionTarget actionTarget,
        IReadOnlyDictionary<string, ReadinessItem> electronicFindings
    )
    {
        var finding = electronicFindings
            .Values.Where(f =>
                OperationalReadinessCodes.IsOwnedByBaseItem(f.Code)
                && OperationalReadinessCodes.ForRequirement(f.Code) == code
            )
            .OrderByDescending(f => f.Severity)
            .FirstOrDefault();

        return finding is null
            ? Item(code, ready, ReadinessSeverity.Warning, NoArea, actionTarget)
            : finding with { Code = code, ActionTarget = actionTarget };
    }

    private static ReadinessSection Section(string code, IReadOnlyList<ReadinessItem> items) =>
        new(code, Aggregate(items), items);

    /// <summary>Missing &gt; Warning &gt; Ready; NotApplicable solo si ningún ítem aplica.</summary>
    private static ReadinessStatus Aggregate(IReadOnlyCollection<ReadinessItem> items)
    {
        if (items.Any(i => i.Status == ReadinessStatus.Missing))
            return ReadinessStatus.Missing;
        if (items.Any(i => i.Status == ReadinessStatus.Warning))
            return ReadinessStatus.Warning;
        return items.Any(i => i.Status == ReadinessStatus.Ready)
            ? ReadinessStatus.Ready
            : ReadinessStatus.NotApplicable;
    }
}
