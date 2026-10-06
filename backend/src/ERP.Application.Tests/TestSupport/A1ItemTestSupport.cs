using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using Moq;

namespace ERP.Application.Tests.TestSupport;

internal static class A1ItemTestSupport
{
    internal static IItemRepository Products(Guid tenant, Guid company)
    {
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdLightAsync(It.IsAny<Guid>(), tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, CancellationToken _) =>
            {
                var item = Item.Create(tenant, "PRODUCT", "Product", "Product", Guid.NewGuid(), "UNIT",
                    ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(),
                    Guid.NewGuid(), companyId: company);
                typeof(Item).GetProperty("Id")!.SetValue(item, id);
                return item;
            });
        return repo.Object;
    }

    internal static IOperationalPreferencesResolver InventoryControl(bool enabled = true)
    {
        var resolver = new Mock<IOperationalPreferencesResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new OperationalPreferences(null!, null!, null!, new InventoryPreferences(false, true, false, 0, enabled), null!, null!, null!));
        return resolver.Object;
    }
}
