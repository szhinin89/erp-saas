using FluentAssertions;
using ERP.Domain.Products.Entities;
// BarcodeType is in ERP.Domain.Products.Entities

namespace ERP.Domain.Tests.Products;

public class ProductTests
{
    [Fact]
    public void Create_should_set_tenant_and_audit_fields()
    {
        var subscriberId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var product = BuildProduct(subscriberId, userId);

        product.SubscriberId.Should().Be(subscriberId);
        product.CreatedBy.Should().Be(userId);
        product.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_should_throw_when_discount_is_out_of_range()
    {
        var act = () => BuildProduct(Guid.NewGuid(), Guid.NewGuid(), maxItemDiscountPercent: 150m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*descuento máximo por ítem*");
    }

    [Fact]
    public void AddBarcode_should_throw_for_duplicate_code()
    {
        var product = BuildProduct(Guid.NewGuid(), Guid.NewGuid());
        var updater = Guid.NewGuid();
        product.AddBarcode("123456", BarcodeType.Internal, updater);

        var act = () => product.AddBarcode("123456", BarcodeType.Internal, updater);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ya existe*");
    }

    private static Product BuildProduct(Guid subscriberId, Guid userId, decimal maxItemDiscountPercent = 0m)
    {
        return Product.Create(
            subscriberId: subscriberId,
            saleCode: "P-100",
            shortName: "Producto base",
            description: "Producto de pruebas",
            lineId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            subcategoryId: Guid.NewGuid(),
            uomCode: "19",
            brandId: Guid.NewGuid(),
            productTypeId: Guid.NewGuid(),
            tariffId: Guid.NewGuid(),
            appliesVatOnSale: false,
            saleVatCode: null,
            saleVatAccountId: null,
            appliesVatOnPurchase: false,
            purchaseVatCode: null,
            purchaseVatAccountId: null,
            createdBy: userId,
            purchaseCode: null,
            appliesExciseTax: false,
            iceCode: null,
            exciseAccountId: null,
            isService: false,
            tracksStock: true,
            tracksLot: false,
            tracksSeries: false,
            hasRecipe: false,
            recipeId: null,
            stockWithDecimal: false,
            saleWithDecimal: false,
            maxItemDiscountPercent: maxItemDiscountPercent,
            availableOnWeb: false,
            availableOnMobile: false,
            isEcommerceActive: false,
            isForSale: true);
    }
}
