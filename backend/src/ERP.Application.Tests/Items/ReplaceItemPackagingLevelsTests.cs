using ERP.Application.Common;
using ERP.Application.Items.UseCases.ItemPackagingLevels;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Items;

public sealed class ReplaceItemPackagingLevelsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly ReplaceItemPackagingLevelsCommandValidator Validator = new();

    private static PackagingLevelInput Level(
        Guid? id,
        string name,
        int level,
        decimal baseQuantity,
        string uomCode,
        bool isBaseUnit
    ) => new(
        id,
        name,
        level,
        baseQuantity,
        uomCode,
        IsBaseUnit: isBaseUnit
    );

    private static Item CreateItem()
    {
        var item = Item.Create(
            TenantId,
            "SKU-001",
            "Item",
            "Item de prueba",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            UserId
        );
        item.ReplacePackagingLevels(
            [
                ("UNIDAD X1", 1, 1m, "UNIT", null, null, true, false, true),
                ("PACA X12", 2, 12m, "PACA", null, null, false, true, false),
            ],
            UserId
        );
        return item;
    }

    [Fact]
    public void Validator_no_exige_unidad_base_sin_contexto_de_stock()
    {
        var cmd = new ReplaceItemPackagingLevelsCommand(
            Guid.NewGuid(),
            [Level(null, "PACA X12", 1, 12m, "PACA", false)]
        );

        var result = Validator.Validate(cmd);

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validator_rechaza_uom_y_cantidad_base_duplicados()
    {
        var cmd = new ReplaceItemPackagingLevelsCommand(
            Guid.NewGuid(),
            [
                Level(null, "UNIDAD X1", 1, 1m, "UNIT", true),
                Level(null, "UNIDAD X1 copia", 2, 1m, "UNIT", false),
            ]
        );

        var result = Validator.Validate(cmd);

        result
            .Errors.Should()
            .Contain(e => e.ErrorMessage.Contains("duplicar UOM y cantidad base"));
    }

    [Fact]
    public async Task Handler_rechaza_quitar_presentacion_usada_por_codigo_proveedor()
    {
        var item = CreateItem();
        var pacaId = item.PackagingLevels.Single(p => p.UomCode == "PACA").Id;
        item.AddSupplierCode("PROV-PACA", true, Guid.NewGuid(), UserId, pacaId);

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.UserId).Returns(UserId);

        var handler = new ReplaceItemPackagingLevelsCommandHandler(
            repo.Object,
            Mock.Of<IPurchaseInvoiceRepository>(),
            tenant.Object,
            user.Object,
            Mock.Of<ISriCatalogResolver>(),
            Mock.Of<IItemTypeRepository>(),
            Mock.Of<IBusinessPartnerRepository>()
        );

        var baseId = item.PackagingLevels.Single(p => p.IsBaseUnit).Id;
        var result = await handler.Handle(
            new ReplaceItemPackagingLevelsCommand(
                item.Id,
                [Level(baseId, "UNIDAD X1", 1, 1m, "UNIT", true)]
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("presentación usada");
        repo.Verify(
            r =>
                r.ReplacePackagingLevelsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IEnumerable<ItemPackagingLevel>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Validator_rechaza_presentacion_base_con_cantidad_distinta_de_uno()
    {
        var cmd = new ReplaceItemPackagingLevelsCommand(
            Guid.NewGuid(),
            [Level(null, "PACA X12", 1, 12m, "PACA", true)]
        );

        var result = Validator.Validate(cmd);

        result
            .Errors.Should()
            .Contain(e => e.ErrorMessage.Contains("cantidad base 1"));
    }

    [Fact]
    public async Task Handler_rechaza_item_inventariable_sin_presentacion_base()
    {
        var item = CreateItem();
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var handler = new ReplaceItemPackagingLevelsCommandHandler(
            repo.Object,
            Mock.Of<IPurchaseInvoiceRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<ISriCatalogResolver>(),
            Mock.Of<IItemTypeRepository>(),
            Mock.Of<IBusinessPartnerRepository>()
        );

        var result = await handler.Handle(
            new ReplaceItemPackagingLevelsCommand(
                item.Id,
                [Level(null, "PACA X12", 1, 12m, "PACA", false)]
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ítems que manejan stock");
    }

    [Fact]
    public async Task Handler_rechaza_cambiar_factor_de_presentacion_usada_en_documentos_confirmados()
    {
        var item = CreateItem();
        var pacaId = item.PackagingLevels.Single(p => p.UomCode == "PACA").Id;
        var baseId = item.PackagingLevels.Single(p => p.IsBaseUnit).Id;

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var purchaseRepo = new Mock<IPurchaseInvoiceRepository>();
        purchaseRepo
            .Setup(r =>
                r.GetPackagingLevelIdsUsedInConfirmedDocumentsAsync(
                    TenantId,
                    item.Id,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new HashSet<Guid> { pacaId });

        var handler = new ReplaceItemPackagingLevelsCommandHandler(
            repo.Object,
            purchaseRepo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<ISriCatalogResolver>(),
            Mock.Of<IItemTypeRepository>(),
            Mock.Of<IBusinessPartnerRepository>()
        );

        var result = await handler.Handle(
            new ReplaceItemPackagingLevelsCommand(
                item.Id,
                [
                    Level(baseId, "UNIDAD X1", 1, 1m, "UNIT", true),
                    Level(pacaId, "PACA X24", 2, 24m, "PACA", false),
                ]
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No se puede cambiar la cantidad base");
    }
}
