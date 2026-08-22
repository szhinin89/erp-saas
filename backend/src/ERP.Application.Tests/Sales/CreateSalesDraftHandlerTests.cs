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
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// Fase 4 (ADR — Rediseño del módulo de Caja) — CreateSalesDraftHandler ya no recibe
/// EmissionPointId del cliente: lo resuelve exclusivamente desde ICurrentCashSession, junto con
/// CashSessionId, y rechaza la operación si el usuario no tiene una caja abierta.
/// </summary>
public sealed class CreateSalesDraftHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

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
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<ICurrentCashSession> CashSession { get; } = new();
        public Mock<IOperationalPreferencesResolver> Preferences { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
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
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);

            var bp = BusinessPartner.Create(
                TenantId,
                "05",
                "1710034065",
                1,
                "Cliente Test",
                UserId
            );
            BpRepo
                .Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(bp);
            // El mock siempre devuelve `bp`, independientemente del Id pedido — suficiente para
            // este test, que solo crea un draft con un único cliente.
            BpRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(bp);

            var role = BusinessPartnerRole.Create(TenantId, bp.Id, RoleType.Customer, UserId);
            RoleRepo
                .Setup(r =>
                    r.GetByTypeAsync(
                        It.IsAny<Guid>(),
                        RoleType.Customer,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(role);

            var pt = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
            PtRepo
                .Setup(r => r.ListAsync(TenantId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PaymentTerm> { pt });
            PtRepo
                .Setup(r =>
                    r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(pt);

            Tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
        }

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
                Tenant.Object,
                Company.Object,
                Branch.Object,
                User.Object,
                CashSession.Object,
                Preferences.Object
            );

        public static CreateSalesDraftCommand ValidCommand() =>
            new(
                CustomerId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }
            );
    }

    [Fact]
    public async Task Con_caja_abierta_crea_la_factura_y_asigna_CashSessionId_y_EmissionPointId_de_la_sesion()
    {
        var f = new Fixture();
        var cashSessionId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(cashSessionId);
        f.CashSession.Setup(c => c.EmissionPointId).Returns(emissionPointId);
        f.EpRepo.Setup(r =>
                r.GetByIdAsync(emissionPointId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ERP.Domain.Modules.Company.Entities.EmissionPoint?)null);

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        captured!.CashSessionId.Should().Be(cashSessionId);
        captured.EmissionPointId.Should().Be(emissionPointId);
        captured.BranchId.Should().Be(BranchId);
    }

    [Fact]
    public async Task POS_DISCOUNT_RULES_01_rechaza_descuento_manual_por_linea_si_no_esta_permitido()
    {
        var f = new Fixture();
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    SalesPos: new SalesPosPreferences(true, false, false, 0m, null, false, false, null, null),
                    Cash: new CashPreferences(true, true, 0m, true, true, true),
                    Purchases: new PurchasesPreferences(null, true, true, true, false),
                    Inventory: new InventoryPreferences(false, true, false, 0m),
                    Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                    ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                    Notifications: new NotificationsPreferences(true, false, "es")
                )
            );
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10", DiscountPct: 10m) }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no permite aplicar descuentos manuales");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task POS_DISCOUNT_RULES_01_rechaza_descuento_manual_por_linea_por_encima_del_tope()
    {
        var f = new Fixture();
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    SalesPos: new SalesPosPreferences(true, false, true, 5m, null, false, false, null, null),
                    Cash: new CashPreferences(true, true, 0m, true, true, true),
                    Purchases: new PurchasesPreferences(null, true, true, true, false),
                    Inventory: new InventoryPreferences(false, true, false, 0m),
                    Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                    ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                    Notifications: new NotificationsPreferences(true, false, "es")
                )
            );
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10", DiscountPct: 10m) }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("descuento máximo permitido es 5");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task POS_DISCOUNT_RULES_01_acepta_descuento_manual_por_linea_dentro_del_tope()
    {
        var f = new Fixture();
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    SalesPos: new SalesPosPreferences(true, false, true, 20m, null, false, false, null, null),
                    Cash: new CashPreferences(true, true, 0m, true, true, true),
                    Purchases: new PurchasesPreferences(null, true, true, true, false),
                    Inventory: new InventoryPreferences(false, true, false, 0m),
                    Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                    ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                    Notifications: new NotificationsPreferences(true, false, "es")
                )
            );
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        var emissionPointId = Guid.NewGuid();
        f.CashSession.Setup(c => c.EmissionPointId).Returns(emissionPointId);
        f.EpRepo.Setup(r =>
                r.GetByIdAsync(emissionPointId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ERP.Domain.Modules.Company.Entities.EmissionPoint?)null);
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10", DiscountPct: 10m) }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task Sin_caja_abierta_rechaza_con_el_mensaje_esperado()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No existe una caja abierta para realizar ventas.");
        f.Repo.Verify(
            r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.BpRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "debe rechazar antes de tocar cualquier otro repositorio si no hay caja abierta"
        );
    }

    // ── SALES-PRESENTATIONS-02 ──────────────────────────────────────────

    private static Item CreateItemWithPackaging()
    {
        var item = Item.Create(
            TenantId,
            "SKU-CAJA",
            "Item con presentación",
            "Item con presentación",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(tracksStock: false),
            UserId
        );
        item.ReplacePackagingLevels(
            [
                ("UNIDAD X1", 1, 1m, "UNIT", null, null, true, false, true),
                ("CAJA X12", 2, 12m, "CAJA", null, null, false, false, false),
            ],
            UserId
        );
        return item;
    }

    [Fact]
    public async Task Con_PackagingLevelId_resuelve_ConversionFactor_y_QuantityInBaseUom()
    {
        var f = new Fixture();
        var item = CreateItemWithPackaging();
        var caja = item.PackagingLevels.Single(p => p.Name == "CAJA X12");

        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.Pricing
            .Setup(p => p.ResolveAsync(item.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.Failure("Sin precio configurado."));
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput>
            {
                new(item.Id, "Caja x12", 2m, 120m, "10", PackagingLevelId: caja.Id),
            }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        var line = captured!.Lines.Single();
        line.PackagingLevelId.Should().Be(caja.Id);
        line.UomCode.Should().Be("CAJA");
        line.BaseUomCode.Should().Be("UNIT");
        line.ConversionFactor.Should().Be(12m);
        line.Quantity.Should().Be(2m);
        line.QuantityInBaseUom.Should().Be(24m);
    }

    [Fact]
    public async Task Sin_PackagingLevelId_preserva_el_comportamiento_actual_venta_en_unidad_base()
    {
        var f = new Fixture();
        var item = CreateItemWithPackaging();

        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.Pricing
            .Setup(p => p.ResolveAsync(item.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.Failure("Sin precio configurado."));
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(Guid.NewGuid());
        f.CashSession.Setup(c => c.EmissionPointId).Returns(Guid.NewGuid());

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var command = new CreateSalesDraftCommand(
            CustomerId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SalesLineInput> { new(item.Id, "Unidad suelta", 5m, 10m, "10") }
        );

        var handler = f.BuildHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = captured!.Lines.Single();
        line.PackagingLevelId.Should().BeNull();
        line.UomCode.Should().Be("UNIT");
        line.ConversionFactor.Should().Be(1m);
        line.QuantityInBaseUom.Should().Be(5m);
    }

    [Fact]
    public void El_cliente_nunca_puede_enviar_EmissionPointId_ni_CashSessionId()
    {
        typeof(CreateSalesDraftCommand)
            .GetProperty("EmissionPointId")
            .Should()
            .BeNull(
                "el punto de emisión se resuelve desde ICurrentCashSession, nunca desde el cliente"
            );
        typeof(CreateSalesDraftCommand)
            .GetProperty("CashSessionId")
            .Should()
            .BeNull(
                "la sesión de caja se resuelve desde ICurrentCashSession, nunca desde el cliente"
            );
    }
}
