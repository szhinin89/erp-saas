using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
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
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<IEstablishmentRepository> EstablishmentRepo { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPointRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ICashRegisterRepository> CashRegisterRepo { get; } = new();
        public Mock<IPriceListRepository> PriceListRepo { get; } = new();
        public Mock<ISriSettingsRepository> SriRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IInvoiceDefaultsResolver> InvoiceDefaultsResolver { get; } = new();
        public Mock<ISalesFiscalPolicyResolver> SalesFiscalPolicyResolver { get; } = new();
        public Mock<ICompanyBrandingResolver> BrandingResolver { get; } = new();
        public Mock<ICatalogConfigurationResolver> CatalogConfigResolver { get; } = new();

        public Fixture()
        {
            CompanyRepo
                .Setup(r => r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CompanyEntity?)null);
            BranchRepo
                .Setup(r =>
                    r.GetAsync(TenantId, true, null, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Array.Empty<Branch>());
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
                    r.GetAsync(TenantId, true, null, null, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Array.Empty<Warehouse>());
            CashRegisterRepo
                .Setup(r =>
                    r.GetAllByCompanyAsync(TenantId, CompanyId, true, null, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Array.Empty<CashRegister>());
            PriceListRepo
                .Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PriceList?)null);
            SriRepo
                .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SriSettings?)null);
            ItemRepo
                .Setup(r => r.GetAllActiveAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ERP.Domain.Modules.Items.Entities.Item>());
            InvoiceDefaultsResolver
                .Setup(r =>
                    r.GetAsync(TenantId, CompanyId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(
                    new InvoiceDefaultsResult(null, null, null, null, null, "None", true, Array.Empty<string>())
                );
            SalesFiscalPolicyResolver
                .Setup(r => r.GetEffectivePolicyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new SalesFiscalPolicyResult(true, 0m, ConsumerFinalMaxAmountSource.Fallback, null)
                );
            BrandingResolver
                .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CompanyBrandingSettings.Empty());
            CatalogConfigResolver
                .Setup(r =>
                    r.ResolveMaxCategoryDepthAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(3);
        }

        public CompanyOperationalReadinessResolver Build() =>
            new(
                CompanyRepo.Object,
                BranchRepo.Object,
                EstablishmentRepo.Object,
                EmissionPointRepo.Object,
                WarehouseRepo.Object,
                CashRegisterRepo.Object,
                PriceListRepo.Object,
                SriRepo.Object,
                ItemRepo.Object,
                InvoiceDefaultsResolver.Object,
                SalesFiscalPolicyResolver.Object,
                BrandingResolver.Object,
                CatalogConfigResolver.Object
            );
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
        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

        result.OverallStatus.Should().Be(ReadinessStatus.Missing);
        result.CanSell.Should().BeFalse();
        result.CanIssueElectronicInvoices.Should().BeFalse();
        result.CanUseInventory.Should().BeFalse();

        var allItems = result.Sections.SelectMany(s => s.Items).ToList();
        allItems
            .Should()
            .Contain(i =>
                i.Code == "identity.taxId"
                && i.Status == ReadinessStatus.Missing
                && i.Severity == ReadinessSeverity.Blocking
            );
    }

    // 2. Empresa con estructura completa → sección estructura Ready.
    [Fact]
    public async Task Empresa_con_estructura_completa_produce_seccion_estructura_Ready()
    {
        var f = new Fixture();
        var branch = BuildMainBranch();
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
        var cashRegister = CashRegister.Create(TenantId, CompanyId, branch.Id, "CAJA-01", "Caja Principal", UserId);

        f.BranchRepo
            .Setup(r => r.GetAsync(TenantId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });
        f.EstablishmentRepo
            .Setup(r => r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(establishment);
        f.EmissionPointRepo
            .Setup(r => r.GetDefaultForCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emissionPoint);
        f.WarehouseRepo
            .Setup(r => r.GetMainForBranchAsync(TenantId, branch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);
        f.WarehouseRepo
            .Setup(r => r.GetAsync(TenantId, true, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { warehouse });
        f.CashRegisterRepo
            .Setup(r => r.GetAllByCompanyAsync(TenantId, CompanyId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { cashRegister });

        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

        var structure = result.Sections.Single(s => s.Code == "structure");
        structure.Status.Should().Be(ReadinessStatus.Ready);
        result.CanUseInventory.Should().BeTrue();
        result.CanUseCashRegister.Should().BeTrue();
    }

    // 3. Sin certificado SRI → CanIssueElectronicInvoices=false.
    [Fact]
    public async Task Sin_certificado_sri_CanIssueElectronicInvoices_es_false()
    {
        var f = new Fixture();
        var sriSettings = SriSettings.Create(
            TenantId,
            CompanyId,
            environment: 1,
            emissionType: 1,
            wsdlUrl: "https://wsdl.example/test",
            createdBy: UserId
        );
        // Sin AttachCertificate: CertP12Path queda null.
        f.SriRepo
            .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sriSettings);

        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

        result.CanIssueElectronicInvoices.Should().BeFalse();
        var certItem = result
            .Sections.Single(s => s.Code == "electronicInvoicing")
            .Items.Single(i => i.Code == "electronicInvoicing.certificate");
        certItem.Status.Should().Be(ReadinessStatus.Missing);
    }

    // 4. Con invoice defaults completos + price list default → sección ventas Ready.
    [Fact]
    public async Task Con_invoice_defaults_completos_y_price_list_default_seccion_ventas_Ready()
    {
        var f = new Fixture();
        f.InvoiceDefaultsResolver
            .Setup(r =>
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
        f.PriceListRepo
            .Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);

        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

        var sales = result.Sections.Single(s => s.Code == "sales");
        sales.Status.Should().Be(ReadinessStatus.Ready);
    }

    // 5. Sin lista de precios default → ventas Missing/Blocking.
    [Fact]
    public async Task Sin_lista_de_precios_default_ventas_queda_Missing_por_item_bloqueante()
    {
        var f = new Fixture();
        f.PriceListRepo
            .Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceList?)null);

        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

        var sales = result.Sections.Single(s => s.Code == "sales");
        sales.Status.Should().Be(ReadinessStatus.Missing);
        var item = sales.Items.Single(i => i.Code == "sales.defaultPriceList");
        item.Status.Should().Be(ReadinessStatus.Missing);
        item.Severity.Should().Be(ReadinessSeverity.Blocking);
        result.CanSell.Should().BeFalse();
    }

    // 6. Bodega default no configurada pero selección manual permitida → Warning, no bloqueo.
    [Fact]
    public async Task Bodega_default_no_configurada_con_seleccion_manual_permitida_es_Warning_no_bloqueante()
    {
        var f = new Fixture();
        f.InvoiceDefaultsResolver
            .Setup(r =>
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

        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

        var item = result
            .Sections.Single(s => s.Code == "sales")
            .Items.Single(i => i.Code == "sales.defaultWarehouse");

        // No hay bodega configurada por sucursal y no hay fallback de bodega principal — es una
        // brecha real (Status=Missing), pero Severity=Warning: nunca bloquea la venta porque la
        // selección manual de bodega siempre está permitida (RequiresManualWarehouseSelection).
        item.Status.Should().Be(ReadinessStatus.Missing);
        item.Severity.Should().Be(ReadinessSeverity.Warning);
        item.Severity.Should().NotBe(ReadinessSeverity.Blocking);
        item.BlockingArea.Should().BeNull();
    }

    // 7. Branding incompleto → Warning, no bloqueo.
    [Fact]
    public async Task Branding_incompleto_es_Warning_nunca_bloqueante()
    {
        var f = new Fixture();
        f.BrandingResolver
            .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyBrandingSettings.Empty());

        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

        var documents = result.Sections.Single(s => s.Code == "documents");
        var brandingItem = documents.Items.Single(i => i.Code == "documents.branding");
        brandingItem.Severity.Should().Be(ReadinessSeverity.Warning);
        brandingItem.BlockingArea.Should().BeNull();
    }

    // 8. Decimales de presentación ausentes → Ready con defaults, no bloqueo.
    [Fact]
    public async Task Decimales_de_presentacion_siempre_Ready_por_fallback_system_default()
    {
        var f = new Fixture();
        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

        var item = result
            .Sections.Single(s => s.Code == "documents")
            .Items.Single(i => i.Code == "documents.presentationDecimals");

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
        var result = await f.Build().GetAsync(TenantId, CompanyId, default);

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
        await f.Build().GetAsync(TenantId, CompanyId, default);

        f.CompanyRepo.Verify(r => r.AddAsync(It.IsAny<CompanyEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        f.BranchRepo.Verify(
            r => r.AddAsync(It.IsAny<Branch>(), It.IsAny<CancellationToken>()),
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
        f.SriRepo.Verify(
            r => r.AddAsync(It.IsAny<SriSettings>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.SriRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.PriceListRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.WarehouseRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.BranchRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.EstablishmentRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.EmissionPointRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.CashRegisterRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.CompanyRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
