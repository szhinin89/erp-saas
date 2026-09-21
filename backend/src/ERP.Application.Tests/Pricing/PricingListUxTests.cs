using ERP.Application.Tests.TestSupport;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Pricing.UseCases.ItemPricingSimulation;
using ERP.Application.Modules.Pricing.UseCases.PriceListItems;
using ERP.Application.Modules.Pricing.UseCases.PricingRules;
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

public sealed class PricingListUxTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IItemRepository> _items = new();
    private readonly Mock<IPriceListRepository> _lists = new();
    private readonly Mock<IPriceListItemRepository> _assignments = new();
    private readonly Mock<IPricingRuleRepository> _rules = new();
    private readonly Mock<ICurrentTenant> _tenant = new();
    private readonly Mock<ICurrentCompany> _company = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<ICompanyClock> _clock = new();

    public PricingListUxTests()
    {
        _tenant.Setup(x => x.TenantId).Returns(_tenantId);
        _company.Setup(x => x.CompanyId).Returns(_companyId);
        _user.Setup(x => x.UserId).Returns(_userId);
        _clock.Setup(x => x.TodayAsync(_companyId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateOnly(2026, 9, 19));
    }

    private Item Item() => ERP.Domain.Modules.Items.Entities.Item.Create(_tenantId, "SKU-1", "Product", "Product",
        Guid.NewGuid(), "UNIT", ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(isForSale: true),
        ItemStockConfig.Create(), _userId, baseSalePrice: 100m);

    private PriceList List() => PriceList.Create(_tenantId, _companyId, "LIST", "List", "USD", false,
        _userId, ruleType: PricingRuleType.PercentMarkup, ruleValue: 20m);

    [Fact]
    public async Task Assigned_items_excludes_inactive_products()
    {
        var list = List();
        var item = Item();
        item.Disable(_userId);
        var assignment = PriceListItem.Create(_tenantId, _companyId, list.Id, item.Id, _userId);
        _assignments.Setup(x => x.GetByPriceListAsync(_tenantId, list.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        _items.Setup(x => x.GetByIdsLightAsync(It.IsAny<IReadOnlyCollection<Guid>>(), _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });
        var result = await new GetItemsAssignedToPriceListHandler(_assignments.Object, _items.Object, _tenant.Object)
            .Handle(new GetItemsAssignedToPriceListQuery(list.Id), CancellationToken.None);
        result.Value.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assigned_items_excludes_missing_and_inactive_assignments(bool inactive)
    {
        var list = List();
        var item = Item();
        var assignment = PriceListItem.Create(_tenantId, _companyId, list.Id, item.Id, _userId);
        assignment.Disable(_userId);
        _assignments.Setup(x => x.GetByPriceListAsync(_tenantId, list.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactive ? new[] { assignment } : Array.Empty<PriceListItem>());
        _items.Setup(x => x.GetByIdsLightAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0), _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Item>());
        var handler = new GetItemsAssignedToPriceListHandler(_assignments.Object, _items.Object, _tenant.Object);
        var result = await handler.Handle(new GetItemsAssignedToPriceListQuery(list.Id), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _items.Verify(x => x.GetByIdsLightAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0), _tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creating_exception_still_rejects_unassigned_or_inactive(bool inactive)
    {
        var list = List();
        var item = Item();
        var assignment = PriceListItem.Create(_tenantId, _companyId, list.Id, item.Id, _userId);
        assignment.Disable(_userId);
        _lists.Setup(x => x.GetByIdAsync(_tenantId, list.Id, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        _assignments.Setup(x => x.FindByKeyAsync(_tenantId, list.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactive ? assignment : null);
        var handler = new SetPricingRuleHandler(_rules.Object, _lists.Object, _assignments.Object, _tenant.Object, _company.Object, _user.Object);
        var result = await handler.Handle(new SetPricingRuleCommand(list.Id, item.Id, PricingRuleType.FixedPrice, 80m), CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        _rules.Verify(x => x.AddAsync(It.IsAny<PricingRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(PricingRuleType.PercentDiscount, 10, 90)]
    [InlineData(PricingRuleType.PercentMarkup, 5, 105)]
    [InlineData(PricingRuleType.FixedAdjustment, -15, 85)]
    [InlineData(PricingRuleType.FixedPrice, 77, 77)]
    public async Task Draft_preview_replaces_general_rule_without_persisting(PricingRuleType type, decimal value, decimal expected)
    {
        var list = List();
        var item = Item();
        var assignment = PriceListItem.Create(_tenantId, _companyId, list.Id, item.Id, _userId);
        _items.Setup(x => x.GetByIdLightAsync(item.Id, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _lists.Setup(x => x.GetAllAsync(_tenantId, true, null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { list });
        _assignments.Setup(x => x.GetByItemAsync(_tenantId, item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { assignment });
        _rules.Setup(x => x.GetByItemAsync(_tenantId, item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PricingRule>());
        var strategies = new PricingAdjustmentStrategyResolver(new IPricingAdjustmentStrategy[] {
            new PercentDiscountStrategy(), new PercentMarkupStrategy(), new FixedAdjustmentStrategy(), new FixedPriceStrategy()
        });
        var handler = new GetItemPricingSimulationQueryHandler(_items.Object, _lists.Object, _rules.Object, _assignments.Object,
            strategies, _tenant.Object, _company.Object, _clock.Object,
            PrecisionPolicyTestDouble.Mock());
        var result = await handler.Handle(new GetItemPricingSimulationQuery(item.Id, ExceptionPreview:
            new SetPricingRuleCommand(list.Id, item.Id, type, value)), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Single().NetPrice.Should().Be(expected);
        result.Value!.Single().RuleSummary.Source.ToString().Should().Be("Exception");
        list.RuleValue.Should().Be(20m);
        _rules.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _rules.Verify(x => x.AddAsync(It.IsAny<PricingRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Preview_validator_reuses_rule_validation_and_rejects_mismatched_item()
    {
        var validator = new GetItemPricingSimulationQueryValidator();
        var query = new GetItemPricingSimulationQuery(Guid.NewGuid(), ExceptionPreview:
            new SetPricingRuleCommand(Guid.NewGuid(), Guid.NewGuid(), PricingRuleType.FixedPrice, -1));
        validator.Validate(query).IsValid.Should().BeFalse();
    }
}
