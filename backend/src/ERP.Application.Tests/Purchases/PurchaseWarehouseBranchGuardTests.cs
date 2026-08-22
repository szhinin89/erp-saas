using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PURCHASE-WAREHOUSE-BRANCH-GUARD-01 — una compra solo puede enviar mercadería a bodegas de su
/// propia sucursal (Warehouse.BranchId == PurchaseInvoice.BranchId). No toca StockMovement,
/// StockRepository, Kardex ni CurrentStock — solo valida en create/update/confirm de Compras.
/// </summary>
public sealed class PurchaseWarehouseBranchGuardTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchA = Guid.NewGuid();
    private static readonly Guid BranchB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WhInBranchA = Guid.NewGuid();
    private static readonly Guid WhInBranchB = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();

    private static Warehouse MakeWarehouse(Guid branchId) =>
        Warehouse.Create(
            TenantId,
            branchId,
            "Bodega",
            "WH-01",
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
            CompanyId
        );

    private static Mock<IWarehouseRepository> BuildWarehouseRepo()
    {
        var whRepo = new Mock<IWarehouseRepository>();
        whRepo
            .Setup(r => r.GetByIdAsync(TenantId, WhInBranchA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWarehouse(BranchA));
        whRepo
            .Setup(r => r.GetByIdAsync(TenantId, WhInBranchB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWarehouse(BranchB));
        return whRepo;
    }

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

    private static CreatePurchaseDraftCommand BuildCreateCommand(Guid warehouseId) =>
        new(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [new PurchaseLineInput(null, "Servicio", 1m, 10m, "10", WarehouseId: warehouseId)],
            GlobalWarehouseId: warehouseId
        );

    [Fact]
    public async Task CreateDraft_con_bodega_de_la_misma_sucursal_retorna_exito()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var handler = new CreatePurchaseDraftHandler(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            BuildSupplierRoleRepo().Object,
            BuildPaymentTermRepo().Object,
            Mock.Of<IItemRepository>(),
            BuildWarehouseRepo().Object,
            BuildTaxResolver().Object,
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchA),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

        var result = await handler.Handle(BuildCreateCommand(WhInBranchA), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        repo.Verify(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDraft_con_bodega_de_otra_sucursal_retorna_error_y_no_persiste()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var handler = new CreatePurchaseDraftHandler(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            BuildSupplierRoleRepo().Object,
            BuildPaymentTermRepo().Object,
            Mock.Of<IItemRepository>(),
            BuildWarehouseRepo().Object,
            Mock.Of<PurchaseTaxResolver>(),
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchA),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

        var result = await handler.Handle(BuildCreateCommand(WhInBranchB), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be(WarehouseBranchGuard.CrossBranchMessage);
        repo.Verify(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PurchaseInvoice CreateExistingDraft(Guid branchId, Guid? warehouseId)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            branchId,
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
            0,
            globalWarehouseId: warehouseId
        );
        if (warehouseId.HasValue)
        {
            var line = PurchaseInvoiceDetail.Create(
                inv.Id,
                TenantId,
                "Servicio",
                1m,
                10m,
                "10",
                "UNIT",
                warehouseId: warehouseId
            );
            inv.ReplaceLines([line], UserId);
        }
        return inv;
    }

    private static UpdatePurchaseDraftHandler BuildUpdateHandler(
        Mock<IPurchaseInvoiceRepository> repo,
        Mock<IWarehouseRepository> whRepo
    ) =>
        new(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            BuildSupplierRoleRepo().Object,
            BuildPaymentTermRepo().Object,
            Mock.Of<IItemRepository>(),
            whRepo.Object,
            BuildTaxResolver().Object,
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

    [Fact]
    public async Task UpdateDraft_con_bodega_de_la_misma_sucursal_retorna_exito()
    {
        var inv = CreateExistingDraft(BranchA, WhInBranchA);
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = BuildUpdateHandler(repo, BuildWarehouseRepo());

        var cmd = new UpdatePurchaseDraftCommand(
            inv.Id,
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [new PurchaseLineInput(null, "Servicio", 1m, 10m, "10", WarehouseId: WhInBranchA)],
            GlobalWarehouseId: WhInBranchA
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task UpdateDraft_con_bodega_de_otra_sucursal_retorna_error_y_no_modifica()
    {
        var inv = CreateExistingDraft(BranchA, WhInBranchA);
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

        var handler = BuildUpdateHandler(repo, BuildWarehouseRepo());

        var cmd = new UpdatePurchaseDraftCommand(
            inv.Id,
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [new PurchaseLineInput(null, "Servicio", 1m, 10m, "10", WarehouseId: WhInBranchB)],
            GlobalWarehouseId: WhInBranchB
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(WarehouseBranchGuard.CrossBranchMessage);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // La compra existente no debe quedar modificada silenciosamente con la bodega inválida.
        inv.GlobalWarehouseId.Should().Be(WhInBranchA);
    }

    [Fact]
    public async Task ConfirmPurchase_con_bodega_de_otra_sucursal_ya_persistida_retorna_error()
    {
        // Simula un draft persistido ANTES de este guard (o alterado fuera del flujo normal):
        // GlobalWarehouseId ya apunta a una bodega de otra sucursal.
        var inv = CreateExistingDraft(BranchA, WhInBranchB);

        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

        var stockRepo = new Mock<IStockRepository>();
        var itemRepo = new Mock<IItemRepository>();
        var whRepo = BuildWarehouseRepo();

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConfirmPurchaseHandlerTests.DefaultOperationalPreferences());

        var handler = new ConfirmPurchaseHandler(
            repo.Object,
            stockRepo.Object,
            itemRepo.Object,
            whRepo.Object,
            Mock.Of<PurchaseTaxResolver>(),
            Mock.Of<IPostingEngine>(),
            Mock.Of<IPricingResolver>(),
            Mock.Of<ILogger<ConfirmPurchaseHandler>>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            preferences.Object
        );

        var result = await handler.Handle(new ConfirmPurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(WarehouseBranchGuard.CrossBranchMessage);
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
        stockRepo.Verify(
            s => s.AppendMovementAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ERP.Domain.Modules.Inventory.Enums.StockMovementType>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Guid?>()
            ),
            Times.Never
        );
    }
}
