using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using EmissionPointEntity = ERP.Domain.Modules.Company.Entities.EmissionPoint;
using EstablishmentEntity = ERP.Domain.Modules.Company.Entities.Establishment;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// COMPANY-OPERATING-SETUP-01 — CompanyOperationalReadinessResolver es pura orquestación de
/// lectura sobre interfaces de dominio (repos + resolvers ya existentes), sin dependencia directa
/// de EF/Postgres — se prueba con dobles Moq, no Testcontainers.
/// </summary>
public sealed class CompanyOperationalReadinessResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<IEstablishmentRepository> EstablishmentRepo { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPointRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ICashRegisterRepository> CashRegisterRepo { get; } = new();
        public Mock<IPriceListRepository> PriceListRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IInvoiceDefaultsResolver> InvoiceDefaultsResolver { get; } = new();
        public Mock<ISalesFiscalPolicyResolver> SalesFiscalPolicyResolver { get; } = new();
        public Mock<ICompanyBrandingResolver> BrandingResolver { get; } = new();
        public Mock<ICatalogConfigurationResolver> CatalogConfigResolver { get; } = new();
        public Mock<IElectronicEmissionRequirementsValidator> EmissionRequirements { get; } = new();
        public Mock<IAccountingOperationalPolicyRepository> AccountingPolicies { get; } = new();
        public Mock<ICompanyClock> CompanyClock { get; } = new();

        /// <summary>Sucursal activa del request (X-Branch-Id ya validado por IBranchAccessGuard).</summary>
        public Branch MainBranch { get; } = BuildMainBranch();

        public Task<CompanyOperationalReadinessResult> GetAsync() =>
            Build().GetAsync(TenantId, CompanyId, MainBranch.Id, default);

        public Fixture()
        {
            CompanyRepo
                .Setup(r =>
                    r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync((CompanyEntity?)null);
            EstablishmentRepo
                .Setup(r =>
                    r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync((EstablishmentEntity?)null);
            EmissionPointRepo
                .Setup(r =>
                    r.GetDefaultForCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync((EmissionPointEntity?)null);
            WarehouseRepo
                .Setup(r =>
                    r.GetAsync(TenantId, true, null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Array.Empty<Warehouse>());
            CashRegisterRepo
                .Setup(r =>
                    r.GetAllByCompanyAsync(
                        TenantId,
                        CompanyId,
                        true,
                        null,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<CashRegister>());
            PriceListRepo
                .Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PriceList?)null);
            EmissionRequirements
                .Setup(v =>
                    v.ValidateAsync(
                        It.IsAny<ElectronicEmissionRequirementsRequest>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new ElectronicEmissionRequirementsResult([]));
            AccountingPolicies
                .Setup(r => r.ListAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<AccountingOperationalPolicy>());
            CompanyClock
                .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DateOnly(2026, 10, 30));
            ItemRepo
                .Setup(r => r.GetAllActiveAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ERP.Domain.Modules.Items.Entities.Item>());
            InvoiceDefaultsResolver
                .Setup(r =>
                    r.GetAsync(
                        TenantId,
                        CompanyId,
                        It.IsAny<Guid?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    new InvoiceDefaultsResult(
                        null,
                        null,
                        null,
                        null,
                        null,
                        "None",
                        true,
                        Array.Empty<string>()
                    )
                );
            SalesFiscalPolicyResolver
                .Setup(r => r.GetEffectivePolicyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new SalesFiscalPolicyResult(
                        true,
                        0m,
                        ConsumerFinalMaxAmountSource.Fallback,
                        null
                    )
                );
            BrandingResolver
                .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CompanyBrandingSettings.Empty());
            CatalogConfigResolver
                .Setup(r =>
                    r.ResolveMaxCategoryDepthAsync(
                        TenantId,
                        CompanyId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(3);
        }

        public CompanyOperationalReadinessResolver Build() =>
            new(
                CompanyRepo.Object,
                EstablishmentRepo.Object,
                EmissionPointRepo.Object,
                WarehouseRepo.Object,
                CashRegisterRepo.Object,
                PriceListRepo.Object,
                ItemRepo.Object,
                InvoiceDefaultsResolver.Object,
                SalesFiscalPolicyResolver.Object,
                BrandingResolver.Object,
                CatalogConfigResolver.Object,
                EmissionRequirements.Object,
                new AccountingOperationalDecision(AccountingPolicies.Object),
                CompanyClock.Object,
                NullLogger<CompanyOperationalReadinessResolver>.Instance
            );

        public void ConfigureAccountingMode(AccountingOperationalMode mode) =>
            AccountingPolicies
                .Setup(r => r.ListAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new[]
                    {
                        AccountingOperationalPolicy.Create(
                            TenantId,
                            CompanyId,
                            mode,
                            new DateOnly(2026, 1, 1),
                            null,
                            UserId
                        ),
                    }
                );

        public void ConfigureEmissionIssues(params ElectronicEmissionRequirementIssue[] issues) =>
            EmissionRequirements
                .Setup(v =>
                    v.ValidateAsync(
                        It.IsAny<ElectronicEmissionRequirementsRequest>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new ElectronicEmissionRequirementsResult(issues));
    }

    private static Branch BuildMainBranch() =>
        Branch.Create(
            TenantId,
            "Matriz",
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            UserId,
            companyId: CompanyId
        );

    // 1. Empresa mínima sin configuración → Missing con items bloqueantes.
    [Fact]
    public async Task Empresa_minima_sin_configuracion_produce_Missing_con_items_bloqueantes()
    {
        var f = new Fixture();
        var result = await f.GetAsync();

        result.OverallStatus.Should().Be(ReadinessStatus.Missing);
        result.CanSell.Should().BeFalse();
        result.CanIssueElectronicInvoices.Should().BeFalse();
        result.CanUseInventory.Should().BeFalse();

        // Bloquean solo los requisitos que exige una operación real: modo contable, caja con punto
        // de emisión y bodega activa. El perfil incompleto (RUC, razón social) es recomendable.
        var allItems = result.Sections.SelectMany(s => s.Items).ToList();
        allItems
            .Where(i => i.Status == ReadinessStatus.Missing)
            .Select(i => i.Code)
            .Should()
            .BeEquivalentTo(
                "accounting.operationalMode",
                "cashRegister.withEmissionPoint",
                "inventory.activeWarehouse"
            );
        allItems.Single(i => i.Code == "identity.taxId").Status.Should().Be(ReadinessStatus.Warning);
    }

    // 2. Empresa con estructura completa → sección estructura Ready.
    [Fact]
    public async Task Empresa_con_estructura_completa_produce_seccion_estructura_Ready()
    {
        var f = new Fixture();
        var branch = f.MainBranch;
        var establishment = EstablishmentEntity.Create(
            TenantId,
            branchId: branch.Id,
            CompanyId,
            code: "001",
            name: "Matriz",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: UserId
        );
        var emissionPoint = EmissionPointEntity.Create(
            TenantId,
            CompanyId,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            createdBy: UserId
        );
        var warehouse = Warehouse.Create(
            TenantId,
            branch.Id,
            "Bodega principal",
            "B01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            UserId,
            CompanyId,
            isMain: true
        );
        f.EstablishmentRepo.Setup(r =>
                r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(establishment);
        f.EmissionPointRepo.Setup(r =>
                r.GetDefaultForCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(emissionPoint);
        f.WarehouseRepo.Setup(r =>
                r.GetMainForBranchAsync(TenantId, branch.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(warehouse);
        ConfigureWarehouses(f, warehouse);
        ConfigureCashRegisters(f, branch, emissionPoint);
        // Inventario y caja contabilizan: sin modo contable definido no están disponibles.
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CompanyEntity.CreateManaged(TenantId, "1790012345001", "Andina S.A."));
        f.ConfigureAccountingMode(AccountingOperationalMode.Enabled);

        var result = await f.GetAsync();

        var structure = result.Sections.Single(s => s.Code == "structure");
        structure.Status.Should().Be(ReadinessStatus.Ready);
        result.CanUseInventory.Should().BeTrue();
        result.CanUseCashRegister.Should().BeTrue();
    }

    // 3. Sin certificado SRI → CanIssueElectronicInvoices=false, con el código del validador.
    [Fact]
    public async Task Sin_certificado_sri_CanIssueElectronicInvoices_es_false()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.ConfigureEmissionIssues(
            new ElectronicEmissionRequirementIssue(
                ElectronicEmissionRequirementCodes.Certificate,
                ElectronicEmissionRequirementSeverity.Required,
                ReadinessActionTarget.ElectronicInvoicingSettings
            )
        );

        var result = await f.GetAsync();

        result.CanIssueElectronicInvoices.Should().BeFalse();
        var certItem = result
            .Sections.Single(s => s.Code == "electronicInvoicing")
            .Items.Single(i => i.Code == ElectronicEmissionRequirementCodes.Certificate);
        certItem.Status.Should().Be(ReadinessStatus.Missing);
        certItem.ActionTarget.Should().Be(ReadinessActionTarget.ElectronicInvoicingSettings);
    }

    // 4. Con invoice defaults completos + price list default → sección ventas Ready.
    [Fact]
    public async Task Con_invoice_defaults_completos_y_price_list_default_seccion_ventas_Ready()
    {
        var f = new Fixture();
        f.InvoiceDefaultsResolver.Setup(r =>
                r.GetAsync(TenantId, CompanyId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new InvoiceDefaultsResult(
                    "01",
                    "01",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "BranchSetting",
                    false,
                    Array.Empty<string>()
                )
            );
        var priceList = PriceList.Create(
            TenantId,
            CompanyId,
            "GEN",
            "Lista general",
            "USD",
            isDefault: true,
            createdBy: UserId
        );
        f.PriceListRepo.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);

        var result = await f.GetAsync();

        var sales = result.Sections.Single(s => s.Code == "sales");
        sales.Status.Should().Be(ReadinessStatus.Ready);
    }

    // 6. Bodega default no configurada pero selección manual permitida → Warning, no bloqueo.
    [Fact]
    public async Task Bodega_default_no_configurada_con_seleccion_manual_permitida_es_Warning_no_bloqueante()
    {
        var f = new Fixture();
        f.InvoiceDefaultsResolver.Setup(r =>
                r.GetAsync(TenantId, CompanyId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new InvoiceDefaultsResult(
                    "01",
                    "01",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "None",
                    RequiresManualWarehouseSelection: true,
                    Array.Empty<string>()
                )
            );

        var result = await f.GetAsync();

        var item = result
            .Sections.Single(s => s.Code == "sales")
            .Items.Single(i => i.Code == "sales.defaultWarehouse");

        // No hay bodega configurada por sucursal y no hay fallback de bodega principal — brecha
        // real pero recomendable (Status=Warning): nunca bloquea la venta porque la selección
        // manual de bodega siempre está permitida (RequiresManualWarehouseSelection).
        item.Status.Should().Be(ReadinessStatus.Warning);
        item.Severity.Should().Be(ReadinessSeverity.Warning);
        item.BlockingAreas.Should().BeEmpty();
    }

    // 7. Branding incompleto → Warning, no bloqueo.
    [Fact]
    public async Task Branding_incompleto_es_Warning_nunca_bloqueante()
    {
        var f = new Fixture();
        f.BrandingResolver.Setup(r =>
                r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CompanyBrandingSettings.Empty());

        var result = await f.GetAsync();

        var documents = result.Sections.Single(s => s.Code == "documents");
        var brandingItem = documents.Items.Single(i => i.Code == "documents.branding");
        brandingItem.Severity.Should().Be(ReadinessSeverity.Warning);
        brandingItem.BlockingAreas.Should().BeEmpty();
    }

    // 8. Precisión decimal: ítem informativo siempre Ready, no bloquea.
    [Fact]
    public async Task Decimales_de_presentacion_siempre_Ready_por_fallback_system_default()
    {
        var f = new Fixture();
        var result = await f.GetAsync();

        var item = result
            .Sections.Single(s => s.Code == "documents")
            .Items.Single(i => i.Code == "documents.decimalPrecision");

        item.Status.Should().Be(ReadinessStatus.Ready);
        item.Severity.Should().Be(ReadinessSeverity.Info);
    }

    // 9. DTO no expone entidades de dominio — verificado a nivel de resolver: el resultado del
    // dominio ya es un record de primitivos/enums/records propios, no entidades EF. La proyección
    // final a ApplicationDTO (con .ToString() de cada enum) se cubre en Application.Tests.
    [Fact]
    public async Task Resultado_del_resolver_no_referencia_tipos_de_entidad_de_dominio()
    {
        var f = new Fixture();
        var result = await f.GetAsync();

        var offendingTypes = result
            .Sections.SelectMany(s => s.Items)
            .Select(i => i.GetType())
            .Distinct()
            .Where(t => t.Namespace != null && t.Namespace.Contains(".Entities"));

        offendingTypes.Should().BeEmpty();
    }

    // 10. No se guarda estado en DB — el resolver es puro lector: ningún AddAsync/SaveChangesAsync
    // de ninguno de los repositorios que compone debe invocarse nunca.
    [Fact]
    public async Task El_resolver_nunca_escribe_en_ningun_repositorio()
    {
        var f = new Fixture();
        await f.GetAsync();

        f.CompanyRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyEntity>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.EstablishmentRepo.Verify(
            r => r.AddAsync(It.IsAny<EstablishmentEntity>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.EmissionPointRepo.Verify(
            r => r.AddAsync(It.IsAny<EmissionPointEntity>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.WarehouseRepo.Verify(
            r => r.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.CashRegisterRepo.Verify(
            r => r.AddAsync(It.IsAny<CashRegister>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.PriceListRepo.Verify(
            r => r.AddAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.AccountingPolicies.Verify(
            r => r.AddAsync(It.IsAny<AccountingOperationalPolicy>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.PriceListRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.WarehouseRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.EstablishmentRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.EmissionPointRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.CashRegisterRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.CompanyRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── FASE 2B-1 ────────────────────────────────────────────────────────────

    private static (Branch Branch, EstablishmentEntity Establishment) ConfigureOperational(
        Fixture f,
        string matrixAddress = "Av. Principal 123"
    )
    {
        var company = CompanyEntity.CreateManaged(
            TenantId,
            "1790012345001",
            "Comercial Andina S.A.",
            tradeName: "Andina",
            corporateEmail: "info@andina.ec"
        );
        var branch = f.MainBranch;
        var establishment = EstablishmentEntity.Create(
            TenantId,
            branchId: branch.Id,
            CompanyId,
            code: "001",
            name: "Matriz",
            address: matrixAddress,
            phone: null,
            isMain: true,
            createdBy: UserId
        );
        var emissionPoint = EmissionPointEntity.Create(
            TenantId,
            CompanyId,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            createdBy: UserId
        );
        var warehouse = Warehouse.Create(
            TenantId, branch.Id, "Bodega principal", "B01",
            null, null, null, null, null, null, null, null, null,
            UserId, CompanyId, isMain: true
        );
        var priceList = PriceList.Create(
            TenantId, CompanyId, "GEN", "Lista general", "USD", isDefault: true, createdBy: UserId
        );
        var item = (ERP.Domain.Modules.Items.Entities.Item)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(ERP.Domain.Modules.Items.Entities.Item)
            );

        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);
        f.EstablishmentRepo.Setup(r =>
                r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(establishment);
        f.EmissionPointRepo.Setup(r =>
                r.GetDefaultForCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(emissionPoint);
        f.WarehouseRepo.Setup(r =>
                r.GetMainForBranchAsync(TenantId, branch.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(warehouse);
        ConfigureWarehouses(f, warehouse);
        ConfigureCashRegisters(f, branch, emissionPoint);
        f.ConfigureAccountingMode(AccountingOperationalMode.Enabled);
        f.PriceListRepo.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        f.ItemRepo.Setup(r => r.GetAllActiveAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });
        f.InvoiceDefaultsResolver.Setup(r =>
                r.GetAsync(TenantId, CompanyId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new InvoiceDefaultsResult(
                    "01", "01", Guid.NewGuid(), emissionPoint.Id, warehouse.Id,
                    "BranchMainWarehouse", false, Array.Empty<string>()
                )
            );
        f.BrandingResolver.Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyBrandingSettings("logos/a.png", null, null, null, "Gracias"));
        return (branch, establishment);
    }

    /// <summary>
    /// Bodegas activas de la empresa; el doble respeta el filtro por sucursal del repositorio real
    /// (branchId null = todas).
    /// </summary>
    private static void ConfigureWarehouses(Fixture f, params Warehouse[] warehouses) =>
        f.WarehouseRepo.Setup(r =>
                r.GetAsync(TenantId, true, null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (Guid _, bool? _, string? _, Guid? branchId, CancellationToken _) =>
                    warehouses.Where(w => branchId is null || w.BranchId == branchId).ToList()
            );

    private static EmissionPointEntity BuildEmissionPoint(
        EstablishmentEntity establishment,
        string code,
        ERP.Domain.Modules.Company.Enums.EmissionType emissionType
    )
    {
        var emissionPoint = EmissionPointEntity.Create(
            TenantId,
            CompanyId,
            establishment.Id,
            code,
            $"PE-{code}",
            emissionType,
            isDefault: false,
            createdBy: UserId
        );
        // Igual que GetByIdForCompanyAsync real (Include(ep => ep.Establishment)).
        typeof(EmissionPointEntity)
            .GetProperty(nameof(EmissionPointEntity.Establishment))!
            .SetValue(emissionPoint, establishment);
        return emissionPoint;
    }

    /// <summary>
    /// Cajas activas de la empresa, cada una con el punto de emisión indicado (null = sin punto).
    /// Los puntos quedan resolubles por GetByIdForCompanyAsync, como en el repositorio real.
    /// </summary>
    private static void ConfigureCashRegisters(
        Fixture f,
        Branch branch,
        params EmissionPointEntity?[] emissionPoints
    )
    {
        var registers = emissionPoints
            .Select(
                (ep, i) =>
                    CashRegister.Create(
                        TenantId,
                        CompanyId,
                        branch.Id,
                        $"CAJA-{i + 1:00}",
                        $"Caja {i + 1}",
                        UserId,
                        emissionPointId: ep?.Id
                    )
            )
            .ToArray();
        foreach (var ep in emissionPoints.OfType<EmissionPointEntity>())
            f.EmissionPointRepo.Setup(r =>
                    r.GetByIdForCompanyAsync(TenantId, CompanyId, ep.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(ep);
        f.CashRegisterRepo.Setup(r =>
                r.GetAllByCompanyAsync(TenantId, CompanyId, true, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(registers);
    }

    [Fact]
    public async Task Ready_es_alcanzable_con_empresa_completamente_configurada()
    {
        var f = new Fixture();
        ConfigureOperational(f);

        var result = await f.GetAsync();

        result
            .Sections.SelectMany(s => s.Items)
            .Where(i => i.Status is ReadinessStatus.Missing or ReadinessStatus.Warning)
            .Select(i => i.Code)
            .Should()
            .BeEmpty();
        result.OverallStatus.Should().Be(ReadinessStatus.Ready);
    }

    [Fact]
    public async Task Ausencias_no_se_reportan_dos_veces_en_secciones_distintas()
    {
        var f = new Fixture();

        var result = await f.GetAsync();

        var items = result.Sections.SelectMany(s => s.Items).ToList();
        items
            .Count(i =>
                i.ActionTarget == ReadinessActionTarget.EmissionPoints
                && i.Status == ReadinessStatus.Missing
            )
            .Should()
            .BeLessThanOrEqualTo(1);
        items.Count(i => i.ActionTarget == ReadinessActionTarget.CashRegisters).Should().Be(1);
        items.Count(i => i.ActionTarget == ReadinessActionTarget.Warehouses).Should().Be(1);
    }

    [Fact]
    public async Task Una_seccion_con_un_item_pendiente_nunca_se_reporta_Ready()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.InvoiceDefaultsResolver.Setup(r =>
                r.GetAsync(TenantId, CompanyId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new InvoiceDefaultsResult(
                    "01", "01", Guid.NewGuid(), Guid.NewGuid(), null, "None", true,
                    Array.Empty<string>()
                )
            );

        var result = await f.GetAsync();

        var sales = result.Sections.Single(s => s.Code == "sales");
        sales.Items.Single(i => i.Code == "sales.defaultWarehouse").Status
            .Should().NotBe(ReadinessStatus.Ready);
        sales.Status.Should().NotBe(ReadinessStatus.Ready);
    }

    [Fact]
    public async Task Direccion_matriz_vacia_bloquea_la_emision_electronica()
    {
        var f = new Fixture();
        ConfigureOperational(f, matrixAddress: "");
        f.ConfigureEmissionIssues(
            new ElectronicEmissionRequirementIssue(
                ElectronicEmissionRequirementCodes.MatrixAddress,
                ElectronicEmissionRequirementSeverity.Required,
                ReadinessActionTarget.Establishments
            )
        );

        var result = await f.GetAsync();

        result.CanIssueElectronicInvoices.Should().BeFalse();
        result.CanSell.Should().BeFalse();
        var item = result
            .Sections.Single(s => s.Code == "electronicInvoicing")
            .Items.Single(i => i.Code == ElectronicEmissionRequirementCodes.MatrixAddress);
        item.Status.Should().Be(ReadinessStatus.Missing);
        item.ActionTarget.Should().Be(ReadinessActionTarget.Establishments);
    }

    private static ElectronicEmissionRequirementIssue Required(
        string code,
        ReadinessActionTarget? target
    ) => new(code, ElectronicEmissionRequirementSeverity.Required, target);

    private static IReadOnlyList<ReadinessItem> AllItems(CompanyOperationalReadinessResult r) =>
        r.Sections.SelectMany(s => s.Items).ToList();

    [Fact]
    public async Task Ruc_provisional_bloquea_venta_y_emision_con_un_solo_requisito()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CompanyEntity.CreateManaged(
                    TenantId,
                    "TMP-EC-1a2b3c4d",
                    "Comercial Andina S.A.",
                    tradeName: "Andina",
                    corporateEmail: "info@andina.ec",
                    isTemporaryTaxIdentification: true,
                    taxIdentificationStatus: ERP.Domain.Modules.Company.Enums.TaxIdentificationStatus.Pending
                )
            );
        f.ConfigureEmissionIssues(
            Required(ElectronicEmissionRequirementCodes.IssuerTaxId, ReadinessActionTarget.CompanyProfile)
        );

        var result = await f.GetAsync();

        var items = AllItems(result);
        var taxId = items.Single(i => i.Code == "identity.taxId");
        taxId.Status.Should().Be(ReadinessStatus.Missing);
        taxId.BlockingAreas.Should().Contain(ReadinessBlockingArea.ElectronicInvoicing);
        items.Should().NotContain(i => i.Code == ElectronicEmissionRequirementCodes.IssuerTaxId);
        result.CanSell.Should().BeFalse();
        result.CanIssueElectronicInvoices.Should().BeFalse();
    }

    [Fact]
    public async Task Ruc_oficial_invalido_para_el_xml_lo_decide_el_validador_en_el_item_del_ruc()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.ConfigureEmissionIssues(
            Required(ElectronicEmissionRequirementCodes.IssuerTaxId, ReadinessActionTarget.CompanyProfile)
        );

        var result = await f.GetAsync();

        // Un dato, un ítem: identity.taxId adopta el veredicto del validador; el código del
        // validador no se publica aparte.
        var items = AllItems(result);
        var taxId = items.Single(i => i.Code == "identity.taxId");
        taxId.Status.Should().Be(ReadinessStatus.Missing);
        taxId.BlockingAreas.Should().Contain(ReadinessBlockingArea.ElectronicInvoicing);
        items.Should().NotContain(i => i.Code == ElectronicEmissionRequirementCodes.IssuerTaxId);
        result.CanIssueElectronicInvoices.Should().BeFalse();
    }

    [Fact]
    public async Task Certificado_vencido_bloquea_la_emision_electronica()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.ConfigureEmissionIssues(
            Required(
                ElectronicEmissionRequirementCodes.CertificateExpired,
                ReadinessActionTarget.ElectronicInvoicingSettings
            )
        );

        var result = await f.GetAsync();

        var item = AllItems(result)
            .Single(i => i.Code == ElectronicEmissionRequirementCodes.CertificateExpired);
        item.Status.Should().Be(ReadinessStatus.Missing);
        item.Severity.Should().Be(ReadinessSeverity.Blocking);
        result.CanIssueElectronicInvoices.Should().BeFalse();
        result.OverallStatus.Should().Be(ReadinessStatus.Missing);
    }

    [Fact]
    public async Task Certificado_proximo_a_vencer_advierte_sin_bloquear()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.ConfigureEmissionIssues(
            new ElectronicEmissionRequirementIssue(
                ElectronicEmissionRequirementCodes.CertificateExpiring,
                ElectronicEmissionRequirementSeverity.Warning,
                ReadinessActionTarget.ElectronicInvoicingSettings
            )
        );

        var result = await f.GetAsync();

        var item = AllItems(result)
            .Single(i => i.Code == ElectronicEmissionRequirementCodes.CertificateExpiring);
        item.Status.Should().Be(ReadinessStatus.Warning);
        item.BlockingAreas.Should().BeEmpty();
        result.CanIssueElectronicInvoices.Should().BeTrue();
        result.CanSell.Should().BeTrue();
        result.OverallStatus.Should().Be(ReadinessStatus.Warning);
    }

    [Fact]
    public async Task Punto_de_emision_invalido_para_el_validador_se_reporta_con_su_codigo()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.ConfigureEmissionIssues(
            Required(ElectronicEmissionRequirementCodes.EmissionPoint, ReadinessActionTarget.EmissionPoints),
            Required(
                ElectronicEmissionRequirementCodes.EstablishmentAddress,
                ReadinessActionTarget.Establishments
            )
        );

        var result = await f.GetAsync();

        var items = AllItems(result);
        items
            .Single(i => i.Code == ElectronicEmissionRequirementCodes.EmissionPoint)
            .ActionTarget.Should()
            .Be(ReadinessActionTarget.EmissionPoints);
        items.Should().Contain(i => i.Code == ElectronicEmissionRequirementCodes.EstablishmentAddress);
        result.CanIssueElectronicInvoices.Should().BeFalse();
    }

    [Fact]
    public async Task El_validador_recibe_el_scope_y_el_punto_de_emision_efectivo()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        var defaultEmissionPointId = (
            await f.EmissionPointRepo.Object.GetDefaultForCompanyAsync(TenantId, CompanyId)
        )!.Id;

        await f.GetAsync();

        f.EmissionRequirements.Verify(
            v =>
                v.ValidateAsync(
                    It.Is<ElectronicEmissionRequirementsRequest>(r =>
                        r.TenantId == TenantId
                        && r.CompanyId == CompanyId
                        && r.EmissionPointId == defaultEmissionPointId
                        && r.DocTypeCode == "01"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Modo_contable_sin_configurar_bloquea_las_operaciones_que_contabilizan()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.AccountingPolicies.Setup(r => r.ListAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AccountingOperationalPolicy>());

        var result = await f.GetAsync();

        var item = AllItems(result).Single(i => i.Code == "accounting.operationalMode");
        item.Status.Should().Be(ReadinessStatus.Missing);
        item.ActionTarget.Should().Be(ReadinessActionTarget.AccountingSettings);
        result.CanSell.Should().BeFalse();
        result.CanUseInventory.Should().BeFalse();
        result.CanUseCashRegister.Should().BeFalse();
        // La factura electrónica nace al autorizar la venta, que contabiliza en su transacción.
        result.CanIssueElectronicInvoices.Should().BeFalse();
        result.OverallStatus.Should().Be(ReadinessStatus.Missing);
    }

    [Fact]
    public async Task Modo_contable_deshabilitado_es_una_decision_valida_y_queda_Ready()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.ConfigureAccountingMode(AccountingOperationalMode.Disabled);

        var result = await f.GetAsync();

        AllItems(result)
            .Single(i => i.Code == "accounting.operationalMode")
            .Status.Should()
            .Be(ReadinessStatus.Ready);
        result.OverallStatus.Should().Be(ReadinessStatus.Ready);
    }

    [Fact]
    public async Task Emision_electronica_no_activada_es_NotApplicable_y_no_bloquea()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        ConfigureCashRegisters(
            f,
            branch,
            BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Physical)
        );

        var result = await f.GetAsync();

        var section = result.Sections.Single(s => s.Code == "electronicInvoicing");
        section.Status.Should().Be(ReadinessStatus.NotApplicable);
        section.Items.Should().ContainSingle(i =>
            i.Code == "electronicInvoicing.activation" && i.Status == ReadinessStatus.NotApplicable
        );
        result.CanIssueElectronicInvoices.Should().BeFalse();
        result.CanSell.Should().BeTrue();
        result.OverallStatus.Should().Be(ReadinessStatus.Ready);
        f.EmissionRequirements.Verify(
            v =>
                v.ValidateAsync(
                    It.IsAny<ElectronicEmissionRequirementsRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Capacidades_son_independientes_entre_si()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        ConfigureWarehouses(f);

        var result = await f.GetAsync();

        // Sin bodega: solo Inventario. La venta y la caja no exigen bodega (SalesWarehouseGuard
        // valida la bodega de cada línea, no la existencia de bodegas en la empresa).
        result.CanUseInventory.Should().BeFalse();
        result.CanSell.Should().BeTrue();
        result.CanUseCashRegister.Should().BeTrue();
        result.CanIssueElectronicInvoices.Should().BeTrue();
        AllItems(result)
            .Single(i => i.Code == "inventory.activeWarehouse")
            .BlockingAreas.Should()
            .Equal(ReadinessBlockingArea.Inventory);
    }

    [Fact]
    public async Task Requisito_del_validador_sobre_un_dato_base_incumplido_no_se_duplica()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyEntity?)null);
        f.ConfigureEmissionIssues(
            Required(ElectronicEmissionRequirementCodes.Company, ReadinessActionTarget.CompanyProfile),
            Required(ElectronicEmissionRequirementCodes.MainEstablishment, ReadinessActionTarget.Establishments)
        );

        var result = await f.GetAsync();

        var items = AllItems(result);
        items.Should().NotContain(i => i.Code == ElectronicEmissionRequirementCodes.Company);
        items.Should().NotContain(i => i.Code == ElectronicEmissionRequirementCodes.MainEstablishment);
        items.Single(i => i.Code == "identity.taxId").Status.Should().Be(ReadinessStatus.Missing);
        items.Single(i => i.Code == "structure.mainEstablishment").Status
            .Should().Be(ReadinessStatus.Missing);
        result.CanIssueElectronicInvoices.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cada_codigo_aparece_una_sola_vez(bool configured)
    {
        var f = new Fixture();
        if (configured)
        {
            ConfigureOperational(f);
            f.ConfigureEmissionIssues(
                Required(ElectronicEmissionRequirementCodes.MatrixAddress, ReadinessActionTarget.Establishments),
                new(
                    ElectronicEmissionRequirementCodes.CertificateExpiring,
                    ElectronicEmissionRequirementSeverity.Warning,
                    ReadinessActionTarget.ElectronicInvoicingSettings
                )
            );
        }

        var result = await f.GetAsync();

        AllItems(result).Select(i => i.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Fallo_tecnico_del_validador_nunca_se_presenta_como_Ready()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.EmissionRequirements.Setup(v =>
                v.ValidateAsync(
                    It.IsAny<ElectronicEmissionRequirementsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new IOException("certificate store unavailable"));

        var result = await f.GetAsync();

        AllItems(result)
            .Single(i => i.Code == "electronicInvoicing.verificationFailed")
            .Status.Should()
            .Be(ReadinessStatus.Missing);
        result.CanIssueElectronicInvoices.Should().BeFalse();
        result.OverallStatus.Should().NotBe(ReadinessStatus.Ready);
    }

    [Fact]
    public async Task Sin_empresa_en_el_scope_no_se_consulta_el_modo_contable()
    {
        var f = new Fixture();

        var result = await f.GetAsync();

        AllItems(result)
            .Single(i => i.Code == "accounting.operationalMode")
            .Status.Should()
            .Be(ReadinessStatus.Missing);
        f.AccountingPolicies.Verify(
            r => r.ListAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.CompanyClock.Verify(
            c => c.TodayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── FASE 2B-1 · coherencia con las operaciones reales ────────────────────

    [Fact]
    public async Task Ruc_provisional_no_impide_vender_desde_una_caja_fisica()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        ConfigureCashRegisters(
            f,
            branch,
            BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Physical)
        );
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CompanyEntity.CreateManaged(
                    TenantId,
                    "TMP-EC-1a2b3c4d",
                    "Comercial Andina S.A.",
                    isTemporaryTaxIdentification: true,
                    taxIdentificationStatus: ERP.Domain.Modules.Company.Enums.TaxIdentificationStatus.Pending
                )
            );

        var result = await f.GetAsync();

        // La autorización física no consulta el RUC del emisor (PhysicalSalesInvoiceEmissionStrategy).
        result.CanSell.Should().BeTrue();
        AllItems(result)
            .Single(i => i.Code == "identity.taxId")
            .BlockingAreas.Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Sin_caja_activa_con_punto_de_emision_no_se_puede_vender()
    {
        var f = new Fixture();
        var (branch, _) = ConfigureOperational(f);
        ConfigureCashRegisters(f, branch, (EmissionPointEntity?)null);

        var result = await f.GetAsync();

        // CreateSalesDraft exige caja abierta y OpenCashSession exige punto de emisión en la caja.
        result.CanSell.Should().BeFalse();
        result.CanUseCashRegister.Should().BeFalse();
        result.CanUseInventory.Should().BeTrue();
        AllItems(result)
            .Single(i => i.Code == "cashRegister.withEmissionPoint")
            .Status.Should()
            .Be(ReadinessStatus.Missing);
    }

    [Fact]
    public async Task El_punto_de_emision_de_la_caja_gobierna_no_el_punto_por_defecto()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f); // punto por defecto: electrónico
        ConfigureCashRegisters(
            f,
            branch,
            BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Physical)
        );
        f.ConfigureEmissionIssues(
            Required(
                ElectronicEmissionRequirementCodes.Certificate,
                ReadinessActionTarget.ElectronicInvoicingSettings
            )
        );

        var result = await f.GetAsync();

        result.CanSell.Should().BeTrue();
        result.CanIssueElectronicInvoices.Should().BeFalse();
        result.Sections.Single(s => s.Code == "electronicInvoicing").Status
            .Should().Be(ReadinessStatus.NotApplicable);
        f.EmissionRequirements.Verify(
            v =>
                v.ValidateAsync(
                    It.IsAny<ElectronicEmissionRequirementsRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Defaults_de_venta_no_son_requisitos_para_vender()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        f.PriceListRepo.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceList?)null);
        f.InvoiceDefaultsResolver.Setup(r =>
                r.GetAsync(TenantId, CompanyId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new InvoiceDefaultsResult(
                    null, null, null, null, Guid.NewGuid(), "BranchMainWarehouse", false,
                    Array.Empty<string>()
                )
            );

        var result = await f.GetAsync();

        // Precio cae a Item.BaseSalePrice, tipo de documento a 01 y condición de pago al Contado
        // del sistema: ninguno bloquea CreateSalesDraft/AuthorizeSales.
        result.CanSell.Should().BeTrue();
        result.OverallStatus.Should().Be(ReadinessStatus.Warning);
        AllItems(result)
            .Where(i => i.Code.StartsWith("sales.", StringComparison.Ordinal))
            .Should()
            .OnlyContain(i => i.BlockingAreas.Count == 0);
    }

    [Fact]
    public async Task Caja_electronica_incompleta_no_bloquea_la_venta_si_hay_caja_fisica()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        ConfigureCashRegisters(
            f,
            branch,
            BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Electronic),
            BuildEmissionPoint(establishment, "003", ERP.Domain.Modules.Company.Enums.EmissionType.Physical)
        );
        f.ConfigureEmissionIssues(
            Required(
                ElectronicEmissionRequirementCodes.Certificate,
                ReadinessActionTarget.ElectronicInvoicingSettings
            )
        );

        var result = await f.GetAsync();

        result.CanSell.Should().BeTrue();
        result.CanIssueElectronicInvoices.Should().BeFalse();
        AllItems(result)
            .Single(i => i.Code == ElectronicEmissionRequirementCodes.Certificate)
            .BlockingAreas.Should()
            .Equal(ReadinessBlockingArea.ElectronicInvoicing);
    }

    [Fact]
    public async Task Bodega_operativa_usa_un_codigo_que_describe_lo_que_comprueba()
    {
        var f = new Fixture();

        var result = await f.GetAsync();

        var codes = AllItems(result).Select(i => i.Code).ToList();
        codes.Should().Contain("inventory.activeWarehouse");
        codes.Should().NotContain("inventory.mainWarehouse");
    }

    // ── FASE 2B-1 · sucursal activa y diagnóstico por caja/punto ─────────────

    private static Branch BuildOtherBranch() =>
        Branch.Create(
            TenantId, "Norte", "Av. Norte 1", "002",
            null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, false, UserId, companyId: CompanyId
        );

    private static ReadinessCashRegister Register(CompanyOperationalReadinessResult r, Guid? emissionPointId) =>
        r.CashRegisters.Single(c => c.EmissionPointId == emissionPointId);

    [Fact]
    public async Task Caja_disponible_solo_en_otra_sucursal_no_habilita_la_venta()
    {
        var f = new Fixture();
        var (_, establishment) = ConfigureOperational(f);
        ConfigureCashRegisters(
            f,
            BuildOtherBranch(),
            BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Physical)
        );

        var result = await f.GetAsync();

        result.BranchId.Should().Be(f.MainBranch.Id);
        result.CanSell.Should().BeFalse();
        result.CanUseCashRegister.Should().BeFalse();
        result.CashRegisters.Should().BeEmpty();
        AllItems(result)
            .Single(i => i.Code == "cashRegister.withEmissionPoint")
            .Status.Should()
            .Be(ReadinessStatus.Missing);
    }

    [Fact]
    public async Task Bodega_disponible_solo_en_otra_sucursal_no_habilita_inventario()
    {
        var f = new Fixture();
        ConfigureOperational(f);
        ConfigureWarehouses(
            f,
            Warehouse.Create(
                TenantId, BuildOtherBranch().Id, "Bodega norte", "B02",
                null, null, null, null, null, null, null, null, null,
                UserId, CompanyId, isMain: true
            )
        );

        var result = await f.GetAsync();

        result.CanUseInventory.Should().BeFalse();
        AllItems(result)
            .Single(i => i.Code == "inventory.activeWarehouse")
            .Status.Should()
            .Be(ReadinessStatus.Missing);
    }

    [Fact]
    public async Task Dos_puntos_electronicos_con_resultados_distintos_se_diagnostican_por_caja()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        var complete = BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Electronic);
        var incomplete = BuildEmissionPoint(establishment, "003", ERP.Domain.Modules.Company.Enums.EmissionType.Electronic);
        ConfigureCashRegisters(f, branch, complete, incomplete);
        f.EmissionRequirements.Setup(v =>
                v.ValidateAsync(
                    It.Is<ElectronicEmissionRequirementsRequest>(r => r.EmissionPointId == incomplete.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ElectronicEmissionRequirementsResult(
                    [Required(ElectronicEmissionRequirementCodes.EstablishmentAddress, ReadinessActionTarget.Establishments)]
                )
            );

        var result = await f.GetAsync();

        var ok = Register(result, complete.Id);
        ok.EmissionPointCode.Should().Be("002");
        ok.EstablishmentCode.Should().Be("001");
        ok.EmissionType.Should().Be(ERP.Domain.Modules.Company.Enums.EmissionType.Electronic);
        ok.OperationalStatus.Should().Be(ReadinessStatus.Ready);
        ok.FiscalStatus.Should().Be(ReadinessFiscalStatus.Verified);
        ok.RequirementCodes.Should().BeEmpty();

        var pending = Register(result, incomplete.Id);
        pending.OperationalStatus.Should().Be(ReadinessStatus.Missing);
        pending.FiscalStatus.Should().Be(ReadinessFiscalStatus.Pending);
        pending.RequirementCodes.Should().Equal(ElectronicEmissionRequirementCodes.EstablishmentAddress);
    }

    [Fact]
    public async Task Un_punto_correcto_no_oculta_el_incumplimiento_de_otro()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        var complete = BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Electronic);
        var incomplete = BuildEmissionPoint(establishment, "003", ERP.Domain.Modules.Company.Enums.EmissionType.Electronic);
        ConfigureCashRegisters(f, branch, complete, incomplete);
        f.EmissionRequirements.Setup(v =>
                v.ValidateAsync(
                    It.Is<ElectronicEmissionRequirementsRequest>(r => r.EmissionPointId == incomplete.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ElectronicEmissionRequirementsResult(
                    [Required(ElectronicEmissionRequirementCodes.EstablishmentAddress, ReadinessActionTarget.Establishments)]
                )
            );

        var result = await f.GetAsync();

        // La capacidad sigue disponible por la caja correcta, pero el requisito queda pendiente.
        result.CanIssueElectronicInvoices.Should().BeTrue();
        result.CanSell.Should().BeTrue();
        var item = AllItems(result)
            .Single(i => i.Code == ElectronicEmissionRequirementCodes.EstablishmentAddress);
        item.Status.Should().Be(ReadinessStatus.Missing);
        item.BlockingAreas.Should().BeEmpty();
        result.OverallStatus.Should().Be(ReadinessStatus.Missing);
    }

    [Fact]
    public async Task Requisito_compartido_se_publica_una_vez_y_cada_caja_lo_referencia()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        var first = BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Electronic);
        var second = BuildEmissionPoint(establishment, "003", ERP.Domain.Modules.Company.Enums.EmissionType.Electronic);
        ConfigureCashRegisters(f, branch, first, second);
        f.ConfigureEmissionIssues(
            Required(ElectronicEmissionRequirementCodes.Certificate, ReadinessActionTarget.ElectronicInvoicingSettings),
            Required(ElectronicEmissionRequirementCodes.IssuerTaxId, ReadinessActionTarget.CompanyProfile)
        );

        var result = await f.GetAsync();

        var items = AllItems(result);
        items.Count(i => i.Code == ElectronicEmissionRequirementCodes.Certificate).Should().Be(1);
        items.Select(i => i.Code).Should().OnlyHaveUniqueItems();
        // El requisito absorbido por un ítem base se referencia con el código de ese ítem.
        Register(result, first.Id).RequirementCodes.Should()
            .BeEquivalentTo(ElectronicEmissionRequirementCodes.Certificate, "identity.taxId");
        Register(result, second.Id).RequirementCodes.Should()
            .BeEquivalentTo(ElectronicEmissionRequirementCodes.Certificate, "identity.taxId");
        result.CanSell.Should().BeFalse();
    }

    [Fact]
    public async Task Punto_compartido_por_dos_cajas_se_valida_una_sola_vez()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        var shared = BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Electronic);
        ConfigureCashRegisters(f, branch, shared, shared);

        var result = await f.GetAsync();

        result.CashRegisters.Should().HaveCount(2);
        f.EmissionRequirements.Verify(
            v =>
                v.ValidateAsync(
                    It.Is<ElectronicEmissionRequirementsRequest>(r => r.EmissionPointId == shared.Id),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Caja_fisica_vende_pero_su_fiscalidad_queda_sin_verificar()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        var physical = BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Physical);
        ConfigureCashRegisters(f, branch, physical);

        var result = await f.GetAsync();

        result.CanSell.Should().BeTrue();
        var register = Register(result, physical.Id);
        register.OperationalStatus.Should().Be(ReadinessStatus.Ready);
        register.FiscalStatus.Should().Be(ReadinessFiscalStatus.NotVerified);
        register.EmissionType.Should().Be(ERP.Domain.Modules.Company.Enums.EmissionType.Physical);
    }

    [Fact]
    public async Task Caja_sin_punto_de_emision_se_diagnostica_aunque_otra_caja_este_lista()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        var physical = BuildEmissionPoint(establishment, "002", ERP.Domain.Modules.Company.Enums.EmissionType.Physical);
        ConfigureCashRegisters(f, branch, physical, null);

        var result = await f.GetAsync();

        result.CanSell.Should().BeTrue();
        var withoutPoint = Register(result, null);
        withoutPoint.OperationalStatus.Should().Be(ReadinessStatus.Missing);
        withoutPoint.FiscalStatus.Should().Be(ReadinessFiscalStatus.NotApplicable);
        withoutPoint.RequirementCodes.Should().Equal("cashRegister.withEmissionPoint");
        var item = AllItems(result).Single(i => i.Code == "cashRegister.withEmissionPoint");
        item.Status.Should().Be(ReadinessStatus.Missing);
        item.BlockingAreas.Should().BeEmpty();
    }

    [Fact]
    public async Task Cajas_de_otra_empresa_nunca_entran_al_diagnostico()
    {
        var f = new Fixture();
        var (branch, establishment) = ConfigureOperational(f);
        var foreignRegister = CashRegister.Create(
            TenantId, Guid.NewGuid(), branch.Id, "CAJA-X", "Ajena", UserId,
            emissionPointId: BuildEmissionPoint(establishment, "009", ERP.Domain.Modules.Company.Enums.EmissionType.Physical).Id
        );
        f.CashRegisterRepo.Setup(r =>
                r.GetAllByCompanyAsync(TenantId, CompanyId, true, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { foreignRegister });

        var result = await f.GetAsync();

        result.CashRegisters.Should().BeEmpty();
        result.CanSell.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_sucursal_activa_no_se_usa_ninguna_caja_ni_bodega()
    {
        var f = new Fixture();
        ConfigureOperational(f);

        var result = await f.Build().GetAsync(TenantId, CompanyId, Guid.Empty, default);

        result.CashRegisters.Should().BeEmpty();
        result.CanSell.Should().BeFalse();
        result.CanUseInventory.Should().BeFalse();
        f.WarehouseRepo.Verify(
            r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
