using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4/§5.1, Subfase 5B) — CreateSalesDraftHandler resuelve
/// ICE/IRBPNR SOLO cuando la empresa es responsable (CompanySpecialTaxResponsibility) Y el ítem
/// tiene la configuración especial activa (ItemSpecialTaxConfiguration) para el mismo
/// SriTaxCategoryCode. Ventas nunca copia impuestos desde Compras — no hay ninguna dependencia de
/// PurchaseInvoiceDetailTax en esta ruta.
/// </summary>
public sealed class SalesDraftSpecialTaxTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISalesInvoiceRepository> Repo { get; } = new();
        public Mock<IBusinessPartnerRepository> BpRepo { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> RoleRepo { get; } = new();
        public Mock<IPaymentTermRepository> PtRepo { get; } = new();
        public Mock<IPaymentMethodRepository> PmRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IEmissionPointRepository> EpRepo { get; } = new();
        public Mock<ISriTaxResolver> Tax { get; } = new();
        public Mock<IPricingResolver> Pricing { get; } = new();
        public Mock<ICompanySpecialTaxResponsibilityRepository> CompanyTaxRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<ICurrentCashSession> CashSession { get; } = new();
        public Mock<IOperationalPreferencesResolver> Preferences { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);
            CashSession.Setup(c => c.HasOpenSession).Returns(true);
            CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
            var emissionPointId = Guid.NewGuid();
            CashSession.Setup(c => c.EmissionPointId).Returns(emissionPointId);
            EpRepo
                .Setup(r => r.GetByIdAsync(emissionPointId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ERP.Domain.Modules.Company.Entities.EmissionPoint?)null);

            Preferences
                .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new OperationalPreferences(
                        SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
                        Cash: new CashPreferences(true, true, 0m, true, true, true),
                        Purchases: new PurchasesPreferences(null, true, true, true, false),
                        Inventory: new InventoryPreferences(false, true, false, 0m),
                        Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                        ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                        Notifications: new NotificationsPreferences(true, false, "es")
                    )
                );

            var bp = BusinessPartner.Create(TenantId, "05", "1710034065", 1, "Cliente Test", UserId);
            BpRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(bp);

            var role = BusinessPartnerRole.Create(TenantId, bp.Id, RoleType.Customer, UserId);
            RoleRepo
                .Setup(r =>
                    r.GetByTypeAsync(It.IsAny<Guid>(), RoleType.Customer, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(role);

            var pt = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
            PtRepo
                .Setup(r => r.ListAsync(TenantId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PaymentTerm> { pt });
            PtRepo
                .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pt);

            Tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));

            Pricing
                .Setup(p =>
                    p.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Result<PricingResult>.Failure("Sin precio configurado."));

            // Default: empresa sin ninguna responsabilidad de impuestos especiales.
            CompanyTaxRepo
                .Setup(r =>
                    r.GetResponsibleSriTaxCategoryCodesAsync(
                        CompanyId,
                        TenantId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<string>());
        }

        public void SetCompanyResponsibleFor(params string[] codes) =>
            CompanyTaxRepo
                .Setup(r =>
                    r.GetResponsibleSriTaxCategoryCodesAsync(
                        CompanyId,
                        TenantId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(codes);

        public CreateSalesDraftHandler BuildHandler() =>
            new(
                Repo.Object,
                BpRepo.Object,
                RoleRepo.Object,
                PtRepo.Object,
                PmRepo.Object,
                ItemRepo.Object,
                EpRepo.Object,
                Tax.Object,
                Pricing.Object,
                CompanyTaxRepo.Object,
                Tenant.Object,
                Company.Object,
                Branch.Object,
                User.Object,
                CashSession.Object,
                Preferences.Object
            );
    }

    private static Item CreateItem(string? iceCatalogCode, string? irbpnrCatalogCode)
    {
        var item = Item.Create(
            TenantId,
            $"SKU-{Guid.NewGuid():N}"[..12],
            "Bebida gaseosa",
            "Bebida gaseosa con azúcar",
            ItemTypeId,
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(tracksStock: false),
            UserId
        );

        var configs = new List<(string, string)>();
        if (iceCatalogCode is not null)
            configs.Add(("3", iceCatalogCode));
        if (irbpnrCatalogCode is not null)
            configs.Add(("5", irbpnrCatalogCode));
        if (configs.Count > 0)
            item.ReplaceSpecialTaxConfigurations(configs, UserId);

        return item;
    }

    private static async Task<(bool IsSuccess, SalesInvoice? Invoice, string? Error)> RunAsync(
        Fixture f,
        Item item,
        decimal quantity = 1m,
        decimal unitPrice = 100m
    )
    {
        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        SalesInvoice? captured = null;
        f.Repo
            .Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(item.Id, "Línea test", quantity, unitPrice, "10") }
        );

        var result = await f.BuildHandler().Handle(command, CancellationToken.None);
        return (result.IsSuccess, captured, result.Error);
    }

    // ── 1. Solo IVA cuando no hay impuestos especiales ──────────────────────

    [Fact]
    public async Task Venta_sin_impuestos_especiales_calcula_solo_IVA()
    {
        var f = new Fixture();
        var item = CreateItem(iceCatalogCode: null, irbpnrCatalogCode: null);

        var (isSuccess, invoice, error) = await RunAsync(f, item);

        isSuccess.Should().BeTrue(error);
        var line = invoice!.Lines.Single();
        line.IceCode.Should().BeNull();
        line.IrbpnrAmount.Should().Be(0m);
        line.Taxes.Should().ContainSingle(t => t.TaxCode == "2");
    }

    // ── 2. Ítem con ICE activo, empresa NO responsable → sin ICE ────────────

    [Fact]
    public async Task Item_con_ICE_activo_pero_empresa_no_responsable_no_calcula_ICE()
    {
        var f = new Fixture();
        var item = CreateItem(iceCatalogCode: "3041", irbpnrCatalogCode: null);
        // CompanyTaxRepo por default (Fixture) no marca ningún código como responsable.

        var (isSuccess, invoice, error) = await RunAsync(f, item);

        isSuccess.Should().BeTrue(error);
        var line = invoice!.Lines.Single();
        line.IceCode.Should().BeNull();
        line.IceAmount.Should().Be(0m);
        f.Tax.Verify(
            t => t.GetIceCatalogEntryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── 3. Empresa responsable de ICE, ítem SIN ICE activo → sin ICE ────────

    [Fact]
    public async Task Empresa_responsable_de_ICE_pero_item_sin_ICE_activo_no_calcula_ICE()
    {
        var f = new Fixture();
        f.SetCompanyResponsibleFor("3");
        var item = CreateItem(iceCatalogCode: null, irbpnrCatalogCode: null);

        var (isSuccess, invoice, error) = await RunAsync(f, item);

        isSuccess.Should().BeTrue(error);
        var line = invoice!.Lines.Single();
        line.IceCode.Should().BeNull();
        line.IceAmount.Should().Be(0m);
    }

    // ── 4. Empresa responsable + ítem con ICE Percentage → calcula y crea fila ──

    [Fact]
    public async Task Empresa_responsable_y_item_con_ICE_Percentage_calcula_ICE_y_crea_SalesInvoiceDetailTax()
    {
        var f = new Fixture();
        f.SetCompanyResponsibleFor("3");
        var item = CreateItem(iceCatalogCode: "3041", irbpnrCatalogCode: null);
        f.Tax
            .Setup(t => t.GetIceCatalogEntryAsync("3041", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriTaxCatalogEntry("3041", "ICE 10%", 10m, null, SriTaxCalculationType.Percentage));

        var (isSuccess, invoice, error) = await RunAsync(f, item, quantity: 1m, unitPrice: 100m);

        isSuccess.Should().BeTrue(error);
        var line = invoice!.Lines.Single();
        line.IceCode.Should().Be("3041");
        line.IceCalculationType.Should().Be(SriTaxCalculationType.Percentage);
        line.IceAmount.Should().Be(10m); // 100 * 10/100
        line.Taxes.Should().Contain(t => t.TaxCode == "3" && t.TaxAmount == 10m);
    }

    // ── 5. Empresa responsable + ítem con ICE Specific → calcula y crea fila ────

    [Fact]
    public async Task Empresa_responsable_y_item_con_ICE_Specific_calcula_ICE_y_crea_SalesInvoiceDetailTax()
    {
        var f = new Fixture();
        f.SetCompanyResponsibleFor("3");
        var item = CreateItem(iceCatalogCode: "3053", irbpnrCatalogCode: null);
        f.Tax
            .Setup(t => t.GetIceCatalogEntryAsync("3053", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriTaxCatalogEntry("3053", "ICE Específico", null, 0.5m, SriTaxCalculationType.Specific)
            );

        var (isSuccess, invoice, error) = await RunAsync(f, item, quantity: 10m, unitPrice: 100m);

        isSuccess.Should().BeTrue(error);
        var line = invoice!.Lines.Single();
        line.IceCode.Should().Be("3053");
        line.IceCalculationType.Should().Be(SriTaxCalculationType.Specific);
        line.IceAmount.Should().Be(5m); // 0.5 * QuantityInBaseUom(10)
        line.Taxes.Should().Contain(t => t.TaxCode == "3" && t.TaxAmount == 5m);
    }

    // ── 6. Empresa responsable + ítem con IRBPNR → calcula y crea fila ──────────

    [Fact]
    public async Task Empresa_responsable_y_item_con_IRBPNR_calcula_IRBPNR_y_crea_SalesInvoiceDetailTax()
    {
        var f = new Fixture();
        f.SetCompanyResponsibleFor("5");
        var item = CreateItem(iceCatalogCode: null, irbpnrCatalogCode: "5001");
        f.Tax
            .Setup(t => t.GetIrbpnrCatalogEntryAsync("5001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriTaxCatalogEntry("5001", "IRBPNR", null, 0.02m, SriTaxCalculationType.Specific));

        var (isSuccess, invoice, error) = await RunAsync(f, item, quantity: 24m, unitPrice: 0.5837m);

        isSuccess.Should().BeTrue(error);
        var line = invoice!.Lines.Single();
        line.IrbpnrCode.Should().Be("5001");
        line.IrbpnrAmount.Should().Be(0.48m); // 0.02 * 24
        line.Taxes.Should().Contain(t => t.TaxCode == "5" && t.TaxAmount == 0.48m);
    }

    // ── 7. Ítem con ICE+IRBPNR activo, empresa responsable SOLO de ICE ──────────

    [Fact]
    public async Task Empresa_responsable_solo_de_ICE_calcula_ICE_pero_no_IRBPNR()
    {
        var f = new Fixture();
        f.SetCompanyResponsibleFor("3"); // NO incluye "5" (IRBPNR)
        var item = CreateItem(iceCatalogCode: "3041", irbpnrCatalogCode: "5001");
        f.Tax
            .Setup(t => t.GetIceCatalogEntryAsync("3041", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriTaxCatalogEntry("3041", "ICE 10%", 10m, null, SriTaxCalculationType.Percentage));

        var (isSuccess, invoice, error) = await RunAsync(f, item);

        isSuccess.Should().BeTrue(error);
        var line = invoice!.Lines.Single();
        line.IceCode.Should().Be("3041");
        line.IceAmount.Should().Be(10m);
        line.IrbpnrCode.Should().BeNull();
        line.IrbpnrAmount.Should().Be(0m);
        f.Tax.Verify(
            t => t.GetIrbpnrCatalogEntryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── 8. Totales correctos (IVA+ICE+IRBPNR) ───────────────────────────────

    [Fact]
    public async Task TaxInclusiveTotal_suma_IVA_ICE_e_IRBPNR_correctamente()
    {
        var f = new Fixture();
        f.SetCompanyResponsibleFor("3", "5");
        var item = CreateItem(iceCatalogCode: "3041", irbpnrCatalogCode: "5001");
        f.Tax
            .Setup(t => t.GetIceCatalogEntryAsync("3041", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriTaxCatalogEntry("3041", "ICE 10%", 10m, null, SriTaxCalculationType.Percentage));
        f.Tax
            .Setup(t => t.GetIrbpnrCatalogEntryAsync("5001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriTaxCatalogEntry("5001", "IRBPNR", null, 0.02m, SriTaxCalculationType.Specific));

        var (isSuccess, invoice, error) = await RunAsync(f, item, quantity: 1m, unitPrice: 100m);

        isSuccess.Should().BeTrue(error);
        var line = invoice!.Lines.Single();
        // TaxableBase=100, ICE=10 (10%), VAT=15% de (100+10)=16.5, IRBPNR=0.02*1=0.02
        line.TaxableBase.Should().Be(100m);
        line.IceAmount.Should().Be(10m);
        line.VatAmount.Should().Be(16.5m);
        line.IrbpnrAmount.Should().Be(0.02m);
        line.TaxInclusiveTotal.Should()
            .Be(line.TaxableBase + line.IceAmount + line.VatAmount + line.IrbpnrAmount);
        line.TaxInclusiveTotal.Should().Be(126.52m);
    }

    // ── 9. Ventas nunca consulta ni copia PurchaseInvoiceDetailTax ──────────

    [Fact]
    public void CreateSalesDraftHandler_no_depende_de_ninguna_abstraccion_de_Purchases()
    {
        var ctor = typeof(CreateSalesDraftHandler).GetConstructors().Single();
        ctor.GetParameters()
            .Should()
            .NotContain(
                p => p.ParameterType.Namespace != null && p.ParameterType.Namespace.Contains("Purchases"),
                "la venta calcula sus impuestos desde ItemSpecialTaxConfiguration/CompanySpecialTaxResponsibility, "
                    + "nunca leyendo PurchaseInvoiceDetailTax ni ningún repositorio de Compras"
            );
    }
}
