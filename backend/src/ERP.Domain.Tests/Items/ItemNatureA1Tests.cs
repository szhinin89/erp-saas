using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Items;

public sealed class ItemNatureA1Tests
{
    [Theory]
    [InlineData(ItemNature.Product, false, false, true, false)]
    [InlineData(ItemNature.Product, false, true, true, false)]
    [InlineData(ItemNature.Product, true, false, true, false)]
    [InlineData(ItemNature.Product, true, true, true, true)]
    [InlineData(ItemNature.Service, false, true, false, false)]
    [InlineData(ItemNature.Service, true, true, false, false)]
    public void Nature_and_two_control_flags_have_independent_responsibilities(ItemNature nature, bool companyControl,
        bool itemControl, bool participates, bool enforces)
    {
        var item = Create(nature, itemControl);
        item.ParticipatesInInventory.Should().Be(participates);
        item.RequiresStockAvailability(companyControl).Should().Be(enforces);
        if (nature == ItemNature.Service) item.StockConfig.StockControlEnabled.Should().BeFalse();
    }

    [Fact]
    public void Service_cannot_enable_inventory_settings_through_update()
    {
        var item = Create(ItemNature.Service, false);
        item.UpdateStockConfig(ItemStockConfig.Create(stockControlEnabled: true, tracksLot: true, tracksSeries: true, minStockQty: 5), Guid.NewGuid());
        item.ParticipatesInInventory.Should().BeFalse();
        item.StockConfig.StockControlEnabled.Should().BeFalse();
        item.StockConfig.TracksLot.Should().BeFalse();
        item.StockConfig.TracksSeries.Should().BeFalse();
        item.StockConfig.MinStockQty.Should().BeNull();
    }

    [Fact]
    public void Missing_company_cannot_create_an_item()
    {
        Action create = () => Item.Create(Guid.NewGuid(), "SKU", "Name", "Description", Guid.NewGuid(), "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(), Guid.NewGuid());
        create.Should().Throw<ArgumentException>();
    }

    private static Item Create(ItemNature nature, bool control) => Item.Create(Guid.NewGuid(), "SKU", "Name", "Description", Guid.NewGuid(), "UNIT",
        ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(stockControlEnabled: control), Guid.NewGuid(), companyId: Guid.NewGuid(), nature: nature);
}
