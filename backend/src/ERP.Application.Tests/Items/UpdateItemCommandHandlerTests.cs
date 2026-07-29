using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Items.UseCases.UpdateItem;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Items;

/// <summary>
/// Regresión: UpdateItemCommandHandler debe hacer merge incremental de los VOs
/// (SaleConfig/TaxConfig), nunca reconstrucción destructiva. Un Update parcial jamás
/// puede resetear IsFavorite a un valor no enviado por el cliente.
/// </summary>
public sealed class UpdateItemCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

    private static Item CreateItem(bool isFavorite, decimal? baseSalePrice = 10m)
    {
        var item = Item.Create(
            TenantId, "SKU-001", "Item de prueba", "Descripción de prueba", ItemTypeId, "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(isFavorite: isFavorite),
            ItemStockConfig.Create(),
            UserId,
            baseSalePrice: baseSalePrice);
        return item;
    }

    private (UpdateItemCommandHandler handler, Mock<IItemRepository> repo) BuildHandler(Item item)
    {
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        repo.Setup(r => r.ExistsBySkuAsync(It.IsAny<string>(), TenantId, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var categoryRepo = new Mock<ICategoryNodeRepository>();
        var itemTypeRepo = new Mock<IItemTypeRepository>();
        itemTypeRepo.Setup(r => r.GetByIdAsync(TenantId, ItemTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemTypeDefinition.Create(TenantId, "FISICO", "Físico", 1, UserId));

        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);

        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.UserId).Returns(UserId);

        var sri = new Mock<ISriCatalogResolver>();
        sri.Setup(s => s.ResolveUomsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriUomInfo>());

        var dbEx = new Mock<IDatabaseExceptionTranslator>();

        var handler = new UpdateItemCommandHandler(
            repo.Object, categoryRepo.Object, itemTypeRepo.Object,
            tenant.Object, user.Object, sri.Object, dbEx.Object);

        return (handler, repo);
    }

    private static UpdateItemCommand ValidCommand(Guid id, decimal baseSalePrice) => new(
        Id: id,
        SKU: "SKU-001",
        ShortName: "Item de prueba",
        Description: "Descripción de prueba",
        DefaultUomCode: "UNIT",
        BaseSalePrice: baseSalePrice);

    [Fact]
    public async Task Update_sin_IsFavorite_preserva_el_valor_existente()
    {
        // Caso 1: item creado con IsFavorite=true, editado sin tocar ese campo.
        var item = CreateItem(isFavorite: true);
        var (handler, _) = BuildHandler(item);

        var result = await handler.Handle(ValidCommand(item.Id, 15m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        item.SaleConfig.IsFavorite.Should().BeTrue("un Update que no envía IsFavorite no debe resetearlo");
    }

    [Fact]
    public async Task Update_parcial_de_precio_no_pierde_IsFavorite()
    {
        // Caso 2: Update parcial (solo precio) no debe perder ningún otro campo ya persistido.
        var item = CreateItem(isFavorite: true, baseSalePrice: 10m);
        var (handler, _) = BuildHandler(item);

        var result = await handler.Handle(ValidCommand(item.Id, 99.99m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        item.BaseSalePrice.Should().Be(99.99m);
        item.SaleConfig.IsFavorite.Should().BeTrue();
    }
}
