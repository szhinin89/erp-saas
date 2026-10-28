using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Items;

/// <summary>
/// Regresión (CI #985): la violación de unicidad al persistir se traduce a Conflict con el código
/// del constraint afectado — barcode → BARCODE_DUPLICATE; cualquier otro → SKU_DUPLICATE.
/// </summary>
public sealed class CreateItemCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

    private static CreateItemCommandHandler BuildHandler(
        string? violatedConstraint,
        Action<Item>? onAdd = null
    )
    {
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()))
            .Callback<Item, CancellationToken>((item, _) => onAdd?.Invoke(item));
        repo.Setup(r => r.ExistsBySkuAsync(It.IsAny<string>(), TenantId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.BarcodeExistsAsync(It.IsAny<string>(), TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        // Carrera: el precheck pasa, pero otra transacción ocupa el código antes del commit.
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Violación de unicidad simulada."));

        var catalog = new Mock<IItemCatalogRepository>();
        catalog.Setup(c => c.BarcodeTypeExistsAndActiveAsync("EAN13", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var itemTypes = new Mock<IItemTypeRepository>();
        itemTypes.Setup(r => r.GetByIdAsync(TenantId, ItemTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemTypeDefinition.Create(TenantId, "FISICO", "Físico", 1, UserId));

        var sri = new Mock<ISriCatalogResolver>();
        sri.Setup(x => x.ResolveVatRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriVatInfo> { ["4"] = new("Venta", 15), ["0"] = new("Compra", 0) });

        var info = new DatabaseUniqueViolationInfo("23505", violatedConstraint, "items", null);
        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out info)).Returns(true);

        return new CreateItemCommandHandler(
            repo.Object,
            Mock.Of<ICategoryNodeRepository>(),
            catalog.Object,
            itemTypes.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            dbEx.Object,
            sri.Object,
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICompanyClock>()
        );
    }

    private static CreateItemCommand ValidCommand() =>
        new(
            SKU: "SKU-001",
            ShortName: "Item de prueba",
            Description: "Descripción de prueba",
            ItemTypeId: ItemTypeId,
            DefaultUomCode: "UNIT",
            CategoryNodeId: null,
            BrandId: null,
            Barcodes: [new CreateItemBarcodeDto("7861234567898", "EAN13", true)],
            SaleVatCode: "4",
            PurchaseVatCode: "0"
        );

    [Fact]
    public async Task Barcode_unique_violation_on_save_returns_BARCODE_DUPLICATE()
    {
        var result = await BuildHandler("uq_item_barcode_tenant_company")
            .Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("BARCODE_DUPLICATE");
        result.Error.Should().Be("Conflicto de unicidad en 'uq_item_barcode_tenant_company'.");
    }

    [Fact]
    public async Task Sku_unique_violation_on_save_returns_SKU_DUPLICATE()
    {
        var result = await BuildHandler("uq_items_tenant_company_sku")
            .Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("SKU_DUPLICATE");
        result.Error.Should().Be("Conflicto de unicidad en 'uq_items_tenant_company_sku'.");
    }

    // R2 H-1 (items.md §5): todo Product nace con su presentación base; un Service no la exige.
    [Fact]
    public async Task Product_is_created_with_its_base_presentation()
    {
        Item? added = null;
        await BuildHandler("uq_items_tenant_company_sku", i => added = i)
            .Handle(ValidCommand(), CancellationToken.None);

        added.Should().NotBeNull();
        var level = added!.PackagingLevels.Should().ContainSingle().Subject;
        level.IsBaseUnit.Should().BeTrue();
        level.BaseQuantity.Should().Be(1m);
        level.Level.Should().Be(1);
        level.UomCode.Should().Be("UNIT");
        level.IsPurchaseDefault.Should().BeTrue();
        level.IsSaleDefault.Should().BeTrue();
        level.Barcode.Should().BeNull();
    }

    [Fact]
    public async Task Service_is_created_without_presentations()
    {
        Item? added = null;
        await BuildHandler("uq_items_tenant_company_sku", i => added = i)
            .Handle(ValidCommand() with { Nature = ItemNature.Service }, CancellationToken.None);

        added.Should().NotBeNull();
        added!.PackagingLevels.Should().BeEmpty();
    }
}
