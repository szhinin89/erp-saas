using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

public sealed class PurchaseDraftPackagingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WhId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();

    [Fact]
    public async Task UpdateDraft_con_presentacion_preservada_mantiene_factor_y_cantidad_base()
    {
        var item = CreateItemWithPackagedPurchasePresentation();
        var packagingId = item.PackagingLevels.Single(x => x.UomCode == "PACA").Id;
        var invoice = CreateDraftInvoice(item.Id, packagingId);

        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "Proveedor", UserId));

        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        itemRepo
            .Setup(r => r.GetSupplierCodeAsync(item.Id, SupplierId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("PROV-PACA-12");

        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));

        var handler = new UpdatePurchaseDraftHandler(
            repo.Object,
            bpRepo.Object,
            Mock.Of<IBusinessPartnerRoleRepository>(),
            Mock.Of<IPaymentTermRepository>(),
            itemRepo.Object,
            Mock.Of<IWarehouseRepository>(),
            tax.Object,
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

        var command = new UpdatePurchaseDraftCommand(
            invoice.Id,
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(
                    item.Id,
                    "Producto PACA",
                    2m,
                    9.29m,
                    "10",
                    WhId,
                    PackagingLevelId: packagingId
                ),
            ],
            GlobalWarehouseId: WhId
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var persistedLine = invoice.Lines.Should().ContainSingle().Subject;
        persistedLine.PackagingLevelId.Should().Be(packagingId);
        persistedLine.ConversionFactor.Should().Be(12m);
        persistedLine.QuantityInBaseUom.Should().Be(24m);
        persistedLine.UomCode.Should().Be("PACA");
        persistedLine.BaseUomCode.Should().Be("UNIT");

        var dtoLine = result.Value!.Lines.Should().ContainSingle().Subject;
        dtoLine.PackagingLevelId.Should().Be(packagingId);
        dtoLine.ConversionFactor.Should().Be(12m);
        dtoLine.QuantityInBaseUom.Should().Be(24m);
        dtoLine.UomCode.Should().Be("PACA");
        dtoLine.BaseUomCode.Should().Be("UNIT");
    }

    [Fact]
    public async Task CreateDraft_con_proveedor_inactivo_retorna_error_especifico_y_no_persiste()
    {
        const string supplierName = "Proveedor Inactivo S.A.";
        var supplier = BusinessPartner.Create(
            TenantId,
            "04",
            "1791352688001",
            2,
            supplierName,
            UserId
        );
        supplier.Deactivate(UserId);

        var repo = new Mock<IPurchaseInvoiceRepository>();
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var handler = new CreatePurchaseDraftHandler(
            repo.Object,
            bpRepo.Object,
            Mock.Of<IBusinessPartnerRoleRepository>(),
            Mock.Of<IPaymentTermRepository>(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            Mock.Of<PurchaseTaxResolver>(),
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

        var result = await handler.Handle(
            new CreatePurchaseDraftCommand(
                SupplierId,
                "01",
                "001-001-000000001",
                DateOnly.FromDateTime(DateTime.UtcNow),
                [new PurchaseLineInput(null, "Servicio", 1m, 10m, "10")]
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be($"El proveedor '{supplierName}' se encuentra inactivo.");
        repo.Verify(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDraft_con_proveedor_inactivo_retorna_error_especifico_y_no_modifica_compra()
    {
        const string supplierName = "Proveedor Inactivo S.A.";
        var supplier = BusinessPartner.Create(
            TenantId,
            "04",
            "1791352688001",
            2,
            supplierName,
            UserId
        );
        supplier.Deactivate(UserId);

        var repo = new Mock<IPurchaseInvoiceRepository>();
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var handler = new UpdatePurchaseDraftHandler(
            repo.Object,
            bpRepo.Object,
            Mock.Of<IBusinessPartnerRoleRepository>(),
            Mock.Of<IPaymentTermRepository>(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            Mock.Of<PurchaseTaxResolver>(),
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>()
        );

        var result = await handler.Handle(
            new UpdatePurchaseDraftCommand(
                Guid.NewGuid(),
                SupplierId,
                "01",
                "001-001-000000001",
                DateOnly.FromDateTime(DateTime.UtcNow),
                [new PurchaseLineInput(null, "Servicio", 1m, 10m, "10")]
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be($"El proveedor '{supplierName}' se encuentra inactivo.");
        repo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Item CreateItemWithPackagedPurchasePresentation()
    {
        var item = Item.Create(
            TenantId,
            "SKU-PACA",
            "Producto PACA",
            "Producto PACA",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            UserId
        );

        item.ReplacePackagingLevels(
            [
                ("Unidad", 1, 1m, "UNIT", null, null, true, false, true),
                ("Paca 12", 2, 12m, "PACA", null, null, false, true, false),
            ],
            UserId
        );

        return item;
    }

    private static PurchaseInvoice CreateDraftInvoice(Guid itemId, Guid packagingId)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor",
            "1790012345001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            0,
            globalWarehouseId: WhId
        );

        var line = PurchaseInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto PACA",
            2m,
            9.29m,
            "10",
            "PACA",
            itemId,
            WhId,
            conversionFactor: 12m,
            baseUomCode: "UNIT",
            packagingLevelId: packagingId
        );
        invoice.ReplaceLines([line], UserId);
        return invoice;
    }
}
