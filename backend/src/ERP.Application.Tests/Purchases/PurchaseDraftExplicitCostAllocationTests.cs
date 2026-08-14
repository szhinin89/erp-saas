using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PURCHASE-DISTRIBUTE-COST-BEFORE-SAVE-01 — cuando el cliente envía FreightAllocated/
/// OtherCostsAllocated explícitos por línea (modal "Distribuir flete/gasto" aplicado ANTES de
/// guardar), Create/UpdatePurchaseDraftHandler deben usarlos tal cual y NO reprorratear con
/// DistributeCosts (que ignora la selección manual de líneas incluidas/excluidas).
/// </summary>
public sealed class PurchaseDraftExplicitCostAllocationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();

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
        return tax;
    }

    private static CreatePurchaseDraftHandler BuildCreateHandler(Mock<IPurchaseInvoiceRepository> repo) =>
        new(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            BuildSupplierRoleRepo().Object,
            BuildPaymentTermRepo().Object,
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            BuildTaxResolver().Object,
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

    [Fact]
    public async Task CreateDraft_con_FreightAllocated_explicito_por_linea_lo_persiste_tal_cual()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var handler = BuildCreateHandler(repo);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(null, "Producto A", 1m, 100m, "10", FreightAllocated: 10m),
                new PurchaseLineInput(null, "Producto B", 1m, 300m, "10", FreightAllocated: 30m),
            ]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var lines = result.Value!.Lines;
        lines.Should().HaveCount(2);
        lines[0].FreightAllocated.Should().Be(10m);
        lines[1].FreightAllocated.Should().Be(30m);
        result.Value.TotalFreight.Should().Be(40m);
    }

    [Fact]
    public async Task CreateDraft_con_asignacion_explicita_solo_en_una_linea_no_toca_la_otra()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var handler = BuildCreateHandler(repo);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(null, "Producto A", 1m, 100m, "10", FreightAllocated: 15m),
                new PurchaseLineInput(null, "Producto B (excluida)", 1m, 100m, "10"),
            ]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].FreightAllocated.Should().Be(15m);
        result.Value.Lines[1].FreightAllocated.Should().Be(0m);
    }

    [Fact]
    public async Task CreateDraft_sin_asignacion_explicita_sigue_usando_DistributeCosts_por_FreightCost()
    {
        // Regresión: si ninguna línea trae FreightAllocated explícito, se preserva el comportamiento
        // histórico (reprorratea cmd.FreightCost proporcionalmente entre TODAS las líneas).
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var handler = BuildCreateHandler(repo);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(null, "Producto A", 1m, 100m, "10"),
                new PurchaseLineInput(null, "Producto B", 1m, 300m, "10"),
            ],
            FreightCost: 40m
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].FreightAllocated.Should().Be(10m);
        result.Value.Lines[1].FreightAllocated.Should().Be(30m);
    }

    private static UpdatePurchaseDraftHandler BuildUpdateHandler(
        Mock<IPurchaseInvoiceRepository> repo
    ) =>
        new(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            BuildSupplierRoleRepo().Object,
            BuildPaymentTermRepo().Object,
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            BuildTaxResolver().Object,
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

    private static PurchaseInvoice CreateExistingDraft()
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
        var line = PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Producto viejo", 1m, 50m, "10", "UNIT");
        inv.ReplaceLines([line], UserId);
        return inv;
    }

    [Fact]
    public async Task UpdateDraft_con_FreightAllocated_explicito_por_linea_lo_persiste_tal_cual()
    {
        var inv = CreateExistingDraft();
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var handler = BuildUpdateHandler(repo);

        var cmd = new UpdatePurchaseDraftCommand(
            inv.Id,
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(null, "Producto A", 1m, 100m, "10", OtherCostsAllocated: 8m),
                new PurchaseLineInput(null, "Producto B", 1m, 400m, "10", OtherCostsAllocated: 32m),
            ]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].OtherCostsAllocated.Should().Be(8m);
        result.Value.Lines[1].OtherCostsAllocated.Should().Be(32m);
        result.Value.TotalOtherCosts.Should().Be(40m);
    }
}
