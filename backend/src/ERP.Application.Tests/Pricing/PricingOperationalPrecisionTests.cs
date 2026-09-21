using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Modules.Pricing.Services;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Pricing;

/// <summary>
/// ERP-PRECISION-OPERATIONAL-05B - la escala del precio unitario de venta que resuelve Pricing
/// viene de CompanyPrecisionPolicy.SalesUnitPriceDecimals (ya no del literal 6). Application resuelve
/// la política; el núcleo puro PricingCalculation solo recibe los dígitos.
/// </summary>
public sealed class PricingOperationalPrecisionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

    private static readonly IPricingAdjustmentStrategyResolver Strategies =
        new PricingAdjustmentStrategyResolver(new IPricingAdjustmentStrategy[]
        {
            new PercentDiscountStrategy(),
            new PercentMarkupStrategy(),
            new FixedAdjustmentStrategy(),
            new FixedPriceStrategy(),
        });

    private static PriceList ListWith(PricingRuleType type, decimal value) =>
        PriceList.Create(TenantId, CompanyId, "L1", "Lista", "USD", isDefault: false, createdBy: UserId, ruleType: type, ruleValue: value);

    [Theory]
    [InlineData(2, 0.67)]
    [InlineData(4, 0.6667)]
    [InlineData(6, 0.666667)]
    public void Resolve_redondea_el_precio_a_los_decimales_recibidos(int decimals, double expected)
    {
        // 1 - 33.333333% = 0.666666667
        var list = ListWith(PricingRuleType.PercentDiscount, 33.333333m);

        var (price, _) = PricingCalculation.Resolve(1m, null, list, Strategies, decimals);

        price.Should().Be((decimal)expected);
    }

    [Fact]
    public void Resolve_precio_negativo_sigue_normalizandose_a_cero()
    {
        var list = ListWith(PricingRuleType.FixedAdjustment, -5m);

        var (price, _) = PricingCalculation.Resolve(1m, null, list, Strategies, 2);

        price.Should().Be(0m);
    }

    private static PricingResolver BuildResolver(Item item, ERP.Application.Modules.Companies.UseCases.PrecisionPolicy.EffectivePrecisionPolicyDto policy)
    {
        var items = new Mock<IItemRepository>();
        items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        var priceLists = new Mock<IPriceListRepository>();
        priceLists
            .Setup(r =>
                r.GetAllAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<PriceList>());
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var provider = new Mock<ICompanyPrecisionPolicyProvider>();
        provider.Setup(p => p.GetEffectiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(policy);

        return new PricingResolver(
            items.Object,
            priceLists.Object,
            new Mock<IPricingRuleRepository>().Object,
            new Mock<IPriceListItemRepository>().Object,
            new Mock<IPriceListSelectionResolver>().Object,
            Strategies,
            tenant.Object,
            company.Object,
            new Mock<ICompanyClock>().Object,
            provider.Object
        );
    }

    private static Item ItemWithBase(decimal basePrice) =>
        Item.Create(
            TenantId, $"SKU-{Guid.NewGuid():N}"[..12], "Item", "Item", ItemTypeId, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(), UserId, baseSalePrice: basePrice
        );

    [Theory]
    [InlineData(2, 1.23)]
    [InlineData(6, 1.234567)]
    public async Task ResolveAsync_precio_base_respeta_salesUnitPriceDecimals_de_la_politica(int decimals, double expected)
    {
        var item = ItemWithBase(1.2345674m);
        var policy = PrecisionPolicyTestDouble.DefaultDto() with { SalesUnitPriceDecimals = decimals };

        var result = await BuildResolver(item, policy).ResolveAsync(item.Id, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnitPrice.Should().Be((decimal)expected);
    }

    [Fact]
    public async Task ResolveAsync_con_perfil_Estandar_redondea_a_2_decimales()
    {
        var item = ItemWithBase(1.005m);

        var result = await BuildResolver(item, PrecisionPolicyTestDouble.DefaultDto())
            .ResolveAsync(item.Id, null, CancellationToken.None);

        result.Value!.UnitPrice.Should().Be(1.01m);
    }
}
