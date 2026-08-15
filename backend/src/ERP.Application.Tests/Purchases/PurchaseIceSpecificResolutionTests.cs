using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using Moq;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PURCHASE-ICE-SPECIFIC-CATALOG-AWARE-FIX-01 — TaxHelper.ResolveTaxesAsync (create/update manual)
/// y RecalculatePurchaseHandler ya no usan el resolver legacy GetIceRateWithNameAsync (que exige
/// Percentage y por eso nunca resuelve ICE "específico" como el código 3053) — ambos ahora usan
/// GetIceCatalogEntryAsync, igual que ConfirmPurchaseUseCases y ReceptionTaxHelper.
/// </summary>
public sealed class PurchaseIceSpecificResolutionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();

    private const string IceSpecificCode = "3053"; // ICE bebidas azucaradas — Specific, UnitValue 0.02
    private const string IcePercentageCode = "3021"; // ICE porcentual genérico de prueba

    private static Mock<IBusinessPartnerRepository> BuildActiveSupplierRepo()
    {
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "Proveedor", UserId)
            );
        return bpRepo;
    }

    private static Mock<IBusinessPartnerRoleRepository> BuildSupplierRoleRepo()
    {
        var roleRepo = new Mock<IBusinessPartnerRoleRepository>();
        var config = SupplierRoleConfig.Create(PtId);
        var role = BusinessPartnerRole.Create(
            TenantId,
            SupplierId,
            Domain.MasterData.Enums.RoleType.Supplier,
            UserId,
            supplierConfig: config
        );
        roleRepo
            .Setup(r =>
                r.GetByTypeAsync(SupplierId, Domain.MasterData.Enums.RoleType.Supplier, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(role);
        return roleRepo;
    }

    private static Mock<IPaymentTermRepository> BuildPaymentTermRepo()
    {
        var ptRepo = new Mock<IPaymentTermRepository>();
        ptRepo
            .Setup(r => r.GetByIdAsync(TenantId, PtId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentTerm.Create(TenantId, "CONTADO", "Contado", 1, 0, UserId));
        return ptRepo;
    }

    private static Mock<PurchaseTaxResolver> BuildTaxResolver()
    {
        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
        tax.Setup(t => t.GetIceCatalogEntryAsync(IceSpecificCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriTaxCatalogEntry(
                    IceSpecificCode,
                    "ICE Bebidas Azucaradas",
                    null,
                    0.02m,
                    SriTaxCalculationType.Specific
                )
            );
        tax.Setup(t => t.GetIceCatalogEntryAsync(IcePercentageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriTaxCatalogEntry(
                    IcePercentageCode,
                    "ICE Porcentual",
                    50m,
                    null,
                    SriTaxCalculationType.Percentage
                )
            );
        // GetIceRateWithNameAsync deliberadamente SIN configurar: si algún call site vivo de Compras
        // todavía lo invocara, Moq devuelve null por defecto y la línea fallaría con
        // "Código ICE no encontrado" — cualquier test que dependiera de él fallaría ruidosamente.
        return tax;
    }

    private static CreatePurchaseDraftHandler BuildCreateHandler(Mock<IPurchaseInvoiceRepository> repo, Mock<PurchaseTaxResolver> tax) =>
        new(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            BuildSupplierRoleRepo().Object,
            BuildPaymentTermRepo().Object,
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            tax.Object,
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

    private static RecalculatePurchaseHandler BuildRecalculateHandler(
        Mock<IPurchaseInvoiceRepository> repo,
        Mock<PurchaseTaxResolver> tax
    ) =>
        new(
            repo.Object,
            tax.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId)
        );

    // ── Caso 1: create manual con ICE específico ────────────────────────────

    [Fact]
    public async Task CreateDraft_linea_manual_con_ICE_especifico_calcula_IceAmount_no_cero()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var tax = BuildTaxResolver();
        var handler = BuildCreateHandler(repo, tax);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [new PurchaseLineInput(null, "Bebida azucarada", 10m, 5m, "10", IceCode: IceSpecificCode)]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines[0];
        // UnitValue (0.02) * QuantityInBaseUom (10, sin empaque) = 0.20 — nunca 0.
        line.IceAmount.Should().Be(0.20m);
        tax.Verify(
            t => t.GetIceRateWithNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── Caso 3 (create): ICE porcentual sin regresión ───────────────────────

    [Fact]
    public async Task CreateDraft_linea_manual_con_ICE_porcentual_no_tiene_regresion()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var tax = BuildTaxResolver();
        var handler = BuildCreateHandler(repo, tax);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [new PurchaseLineInput(null, "Producto con ICE %", 1m, 100m, "10", IceCode: IcePercentageCode)]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines[0];
        // TaxableBase (100) * 50% = 50.
        line.IceAmount.Should().Be(50m);
    }

    // ── Caso 4 (create): sin ICE sin regresión ──────────────────────────────

    [Fact]
    public async Task CreateDraft_linea_manual_sin_ICE_no_tiene_regresion()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var tax = BuildTaxResolver();
        var handler = BuildCreateHandler(repo, tax);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [new PurchaseLineInput(null, "Producto sin ICE", 1m, 100m, "10")]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].IceAmount.Should().Be(0m);
    }

    // ── Caso 2: RecalculatePurchaseHandler con ICE específico ───────────────

    private static PurchaseInvoice CreateExistingDraftWithLine(PurchaseInvoiceDetail line)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor",
            "1791352688001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            0
        );
        inv.ReplaceLines([line], UserId);
        return inv;
    }

    [Fact]
    public async Task Recalculate_linea_con_ICE_especifico_preserva_IceAmount_no_lo_pierde()
    {
        // Línea ya creada con el monto exacto correcto (p. ej. vía Recepción XML) — Recalculate debe
        // preservarlo, nunca recalcularlo desde una tarifa porcentual ni resetearlo a 0.
        var line = PurchaseInvoiceDetail.Create(
            Guid.NewGuid(),
            TenantId,
            "Bebida azucarada",
            10m,
            5m,
            "10",
            "UNIT",
            iceCode: IceSpecificCode
        );
        line.ApplyTaxes(
            "10",
            15m,
            "IVA 15%",
            IceSpecificCode,
            0m,
            "ICE Bebidas Azucaradas",
            SriTaxCalculationType.Specific,
            0.20m
        );
        line.IceAmount.Should().Be(0.20m); // precondición del test

        var inv = CreateExistingDraftWithLine(line);
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tax = BuildTaxResolver();
        var handler = BuildRecalculateHandler(repo, tax);

        var result = await handler.Handle(new RecalculatePurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].IceAmount.Should().Be(0.20m);
        tax.Verify(
            t => t.GetIceRateWithNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Recalculate_linea_con_ICE_porcentual_no_tiene_regresion()
    {
        var line = PurchaseInvoiceDetail.Create(
            Guid.NewGuid(),
            TenantId,
            "Producto con ICE %",
            1m,
            100m,
            "10",
            "UNIT",
            iceCode: IcePercentageCode
        );
        var inv = CreateExistingDraftWithLine(line);
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tax = BuildTaxResolver();
        var handler = BuildRecalculateHandler(repo, tax);

        var result = await handler.Handle(new RecalculatePurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].IceAmount.Should().Be(50m);
    }

    [Fact]
    public async Task Recalculate_linea_sin_ICE_no_tiene_regresion()
    {
        var line = PurchaseInvoiceDetail.Create(
            Guid.NewGuid(),
            TenantId,
            "Producto sin ICE",
            1m,
            100m,
            "10",
            "UNIT"
        );
        var inv = CreateExistingDraftWithLine(line);
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tax = BuildTaxResolver();
        var handler = BuildRecalculateHandler(repo, tax);

        var result = await handler.Handle(new RecalculatePurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].IceAmount.Should().Be(0m);
    }
}
