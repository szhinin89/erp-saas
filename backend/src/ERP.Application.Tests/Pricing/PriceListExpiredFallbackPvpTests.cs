using ERP.Application.Tests.TestSupport;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
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
/// PRICE-LIST-EXPIRED-FALLBACK-PVP-01 — una PriceList vencida, futura, deshabilitada o
/// inexistente NUNCA debe bloquear la venta: se ignora y el precio cae al PVP/BaseSalePrice del
/// ítem. El único bloqueo real de <see cref="PricingResolver"/> es la ausencia de un
/// <c>Item.BaseSalePrice</c> válido. ValidFrom/ValidTo/IsValidOn (semántica inclusiva) no se
/// tocan — solo cambia qué hace el resolver cuando la lista no aplica.
/// </summary>
public sealed class PriceListExpiredFallbackPvpTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();
    private static readonly DateOnly CompanyToday = new(2026, 9, 17);

    private sealed class Fixture
    {
        public Mock<IItemRepository> Items { get; } = new();
        public Mock<IPriceListRepository> PriceLists { get; } = new();
        public Mock<IPricingRuleRepository> Rules { get; } = new();
        public Mock<IPriceListItemRepository> Assignments { get; } = new();
        public Mock<IPriceListSelectionResolver> Selection { get; } = new();
        public Mock<IPricingAdjustmentStrategyResolver> Strategies { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICompanyClock> CompanyClock { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            CompanyClock
                .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CompanyToday);
            Rules
                .Setup(r =>
                    r.GetActiveForItemInListAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((PricingRule?)null);
            // Por defecto, asignado y activo — los tests de vigencia de esta suite no cubren
            // asignación (eso lo cubre PricingListAssignmentEnforcementTests). Los tests de
            // asignación de este archivo sobreescriben este setup explícitamente.
            Assignments
                .Setup(a =>
                    a.FindByKeyAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(PriceListItem.Create(TenantId, CompanyId, Guid.NewGuid(), Guid.NewGuid(), UserId));
        }

        public PricingResolver Build() =>
            new(
                Items.Object,
                PriceLists.Object,
                Rules.Object,
                Assignments.Object,
                Selection.Object,
                Strategies.Object,
                Tenant.Object,
                Company.Object,
                CompanyClock.Object,
                PrecisionPolicyTestDouble.Mock()
            );
    }

    private static Item CreateItem(decimal? basePrice = 24.30m) =>
        Item.Create(
            TenantId,
            "SKU-001",
            "Item de prueba",
            "Item de prueba",
            ItemTypeId,
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(),
            UserId,
            baseSalePrice: basePrice
        );

    private static PriceList CreatePriceList(
        DateOnly? validFrom,
        DateOnly? validUntil,
        PricingRuleType? ruleType = null,
        decimal? ruleValue = null
    ) =>
        PriceList.Create(
            TenantId,
            CompanyId,
            "GENERAL",
            "Lista General",
            "USD",
            isDefault: true,
            createdBy: UserId,
            validFrom: validFrom,
            validUntil: validUntil,
            ruleType: ruleType,
            ruleValue: ruleValue
        );

    [Fact]
    public async Task Lista_vigente_aplica_el_ajuste_configurado()
    {
        var f = new Fixture();
        var strategy = new Mock<IPricingAdjustmentStrategy>();
        strategy
            .Setup(s => s.Apply(It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns((decimal basePrice, decimal pct) => basePrice * (1 - pct / 100m));
        f.Strategies.Setup(s => s.Resolve(PricingRuleType.PercentDiscount)).Returns(strategy.Object);

        var item = CreateItem(100m);
        var priceList = CreatePriceList(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            PricingRuleType.PercentDiscount,
            10m
        );
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().Be(priceList.Id);
        result.Value!.RuleApplied.Should().NotBeNull();
        result.Value!.UnitPrice.Should().Be(90m);
    }

    [Fact]
    public async Task Lista_vencida_se_ignora_y_usa_PVP_base_sin_error()
    {
        var f = new Fixture();
        var item = CreateItem(24.30m);
        var priceList = CreatePriceList(
            new DateOnly(2026, 9, 9),
            new DateOnly(2026, 9, 10), // venció ayer respecto a CompanyToday (17/09)
            PricingRuleType.PercentDiscount,
            10m
        );
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().BeNull();
        result.Value!.RuleApplied.Should().BeNull();
        result.Value!.RuleDescription.Should().BeNull();
        result.Value!.UnitPrice.Should().Be(24.30m);
        result.Value!.BasePrice.Should().Be(24.30m);
    }

    [Fact]
    public async Task Lista_futura_se_ignora_y_usa_PVP_base_sin_error()
    {
        var f = new Fixture();
        var item = CreateItem(24.30m);
        var priceList = CreatePriceList(
            new DateOnly(2026, 10, 1), // aún no empieza respecto a CompanyToday (17/09)
            new DateOnly(2026, 10, 31),
            PricingRuleType.PercentDiscount,
            10m
        );
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().BeNull();
        result.Value!.UnitPrice.Should().Be(24.30m);
    }

    [Fact]
    public async Task Sin_lista_predeterminada_ni_explicita_usa_PVP_base_sin_error()
    {
        var f = new Fixture();
        var item = CreateItem(24.30m);
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetAllAsync(TenantId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceList>());

        var result = await f.Build().ResolveAsync(item.Id, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().BeNull();
        result.Value!.UnitPrice.Should().Be(24.30m);
        result.Value!.PriceListCode.Should().Be("PVP");
    }

    [Fact]
    public async Task Lista_explicita_inexistente_usa_PVP_base_sin_error()
    {
        var f = new Fixture();
        var item = CreateItem(24.30m);
        var missingListId = Guid.NewGuid();
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, missingListId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceList?)null);

        var result = await f.Build().ResolveAsync(item.Id, missingListId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnitPrice.Should().Be(24.30m);
    }

    [Fact]
    public async Task Lista_deshabilitada_se_ignora_y_usa_PVP_base_sin_error()
    {
        var f = new Fixture();
        var item = CreateItem(24.30m);
        var priceList = CreatePriceList(null, null);
        priceList.Disable(UserId);
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnitPrice.Should().Be(24.30m);
    }

    [Fact]
    public async Task Sin_PVP_base_valido_si_falla()
    {
        var f = new Fixture();
        var item = CreateItem(basePrice: null);
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await f.Build().ResolveAsync(item.Id, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.PriceLists.Verify(
            r => r.GetAllAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
