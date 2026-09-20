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
/// PRICING-LIST-ASSIGNMENT-ENFORCEMENT-02 — una PriceList (vigente y activa) solo aplica su
/// regla (general o excepción) a un ítem que tenga una <see cref="PriceListItem"/> ACTIVA para
/// exactamente (PriceListId, ItemId, Tenant, Company). Sin esa asignación, el resultado es el
/// mismo fallback de PVP/BaseSalePrice que ya usa <see cref="PriceListExpiredFallbackPvpTests"/>
/// para lista inactiva/vencida — la asignación deja de ser solo una compuerta administrativa
/// para crear excepciones (ver SetPricingRuleHandler) y pasa a gobernar también si la regla
/// general de la lista aplica al resolver el precio real de venta/simulación.
/// </summary>
public sealed class PricingListAssignmentEnforcementTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();
    private static readonly DateOnly CompanyToday = new(2026, 9, 19);

    private sealed class Fixture
    {
        public Mock<IItemRepository> Items { get; } = new();
        public Mock<IPriceListRepository> PriceLists { get; } = new();
        public Mock<IPricingRuleRepository> Rules { get; } = new();
        public Mock<IPriceListItemRepository> Assignments { get; } = new();
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
        }

        public PricingResolver Build() =>
            new(
                Items.Object,
                PriceLists.Object,
                Rules.Object,
                Assignments.Object,
                Strategies.Object,
                Tenant.Object,
                Company.Object,
                CompanyClock.Object
            );
    }

    private static Item CreateItem(decimal basePrice = 100m) =>
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
        PricingRuleType? ruleType = PricingRuleType.PercentMarkup,
        decimal? ruleValue = 20m,
        bool isDefault = true
    ) =>
        PriceList.Create(
            TenantId,
            CompanyId,
            "GENERAL",
            "Lista General",
            "USD",
            isDefault: isDefault,
            createdBy: UserId,
            ruleType: ruleType,
            ruleValue: ruleValue
        );

    private static IPricingAdjustmentStrategyResolver RealStrategies() =>
        new PricingAdjustmentStrategyResolver(new IPricingAdjustmentStrategy[]
        {
            new PercentDiscountStrategy(),
            new PercentMarkupStrategy(),
            new FixedAdjustmentStrategy(),
            new FixedPriceStrategy(),
        });

    private void SetupItemAndList(Fixture f, Item item, PriceList priceList)
    {
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        f.PriceLists
            .Setup(r => r.GetAllAsync(TenantId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { priceList });
    }

    [Fact]
    public async Task Item_asignado_y_activo_aplica_la_regla_general_de_la_lista()
    {
        var f = new Fixture();
        var strategies = RealStrategies();
        var item = CreateItem(100m);
        var priceList = CreatePriceList(PricingRuleType.PercentMarkup, 20m);
        SetupItemAndList(f, item, priceList);
        f.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PriceListItem.Create(TenantId, CompanyId, priceList.Id, item.Id, UserId));
        f.Rules
            .Setup(r => r.GetActiveForItemInListAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingRule?)null);
        f.Strategies.Setup(s => s.Resolve(It.IsAny<PricingRuleType>()))
            .Returns((PricingRuleType t) => strategies.Resolve(t));

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().Be(priceList.Id);
        result.Value!.UnitPrice.Should().Be(120m);
        result.Value!.RuleApplied.Should().NotBeNull();
    }

    [Fact]
    public async Task Item_asignado_con_excepcion_activa_la_excepcion_gana_sobre_la_regla_general()
    {
        var f = new Fixture();
        var strategies = RealStrategies();
        var item = CreateItem(100m);
        var priceList = CreatePriceList(PricingRuleType.PercentMarkup, 20m);
        SetupItemAndList(f, item, priceList);
        f.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PriceListItem.Create(TenantId, CompanyId, priceList.Id, item.Id, UserId));
        var exception = PricingRule.Create(TenantId, CompanyId, priceList.Id, item.Id, PricingRuleType.PercentDiscount, 10m, UserId);
        f.Rules
            .Setup(r => r.GetActiveForItemInListAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exception);
        f.Strategies.Setup(s => s.Resolve(It.IsAny<PricingRuleType>()))
            .Returns((PricingRuleType t) => strategies.Resolve(t));

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        // Excepción (10% descuento) gana sobre la regla general (20% recargo) → 90, nunca 120.
        result.Value!.UnitPrice.Should().Be(90m);
        result.Value!.RuleApplied.Should().Contain("ítem");
    }

    [Fact]
    public async Task Item_sin_asignacion_en_la_lista_usa_PVP_base()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var priceList = CreatePriceList(PricingRuleType.PercentMarkup, 20m);
        SetupItemAndList(f, item, priceList);
        f.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceListItem?)null);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().BeNull();
        result.Value!.PriceListCode.Should().Be("PVP");
        result.Value!.UnitPrice.Should().Be(100m);
        result.Value!.RuleApplied.Should().BeNull();
        f.Rules.Verify(
            r => r.GetActiveForItemInListAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Asignacion_deshabilitada_usa_PVP_base()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var priceList = CreatePriceList(PricingRuleType.PercentMarkup, 20m);
        SetupItemAndList(f, item, priceList);
        var assignment = PriceListItem.Create(TenantId, CompanyId, priceList.Id, item.Id, UserId);
        assignment.Disable(UserId);
        f.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignment);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().BeNull();
        result.Value!.UnitPrice.Should().Be(100m);
    }

    [Fact]
    public async Task Item_asignado_a_otra_lista_no_habilita_la_regla_de_esta_lista()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var priceList = CreatePriceList(PricingRuleType.PercentMarkup, 20m);
        var otherListId = Guid.NewGuid();
        SetupItemAndList(f, item, priceList);
        // Activo en OTRA lista — no en la que se está resolviendo.
        f.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, otherListId, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PriceListItem.Create(TenantId, CompanyId, otherListId, item.Id, UserId));
        f.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceListItem?)null);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().BeNull();
        result.Value!.UnitPrice.Should().Be(100m);
    }

    [Fact]
    public async Task Lista_vencida_o_inactiva_usa_PVP_sin_siquiera_consultar_la_asignacion()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var priceList = CreatePriceList(PricingRuleType.PercentMarkup, 20m);
        priceList.Disable(UserId);
        SetupItemAndList(f, item, priceList);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnitPrice.Should().Be(100m);
        // Orden correcto: vigencia se evalúa ANTES que asignación — una lista que ya no aplica
        // nunca debería disparar una consulta extra de asignación.
        f.Assignments.Verify(
            a => a.FindByKeyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Lista_explicita_y_lista_default_aplican_la_misma_verificacion_de_asignacion()
    {
        var strategies = RealStrategies();

        var fExplicit = new Fixture();
        var item = CreateItem(100m);
        var priceList = CreatePriceList(PricingRuleType.PercentMarkup, 20m, isDefault: true);
        SetupItemAndList(fExplicit, item, priceList);
        fExplicit.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceListItem?)null);

        var resultExplicit = await fExplicit.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        var fDefault = new Fixture();
        SetupItemAndList(fDefault, item, priceList);
        fDefault.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceListItem?)null);

        var resultDefault = await fDefault.Build().ResolveAsync(item.Id, null, CancellationToken.None);

        resultExplicit.IsSuccess.Should().BeTrue();
        resultDefault.IsSuccess.Should().BeTrue();
        resultExplicit.Value!.UnitPrice.Should().Be(resultDefault.Value!.UnitPrice);
        resultExplicit.Value!.PriceListId.Should().BeNull();
        resultDefault.Value!.PriceListId.Should().BeNull();
    }

    [Fact]
    public async Task Verifica_asignacion_con_el_TenantId_ambiental_fail_closed()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var priceList = CreatePriceList(PricingRuleType.PercentMarkup, 20m);
        SetupItemAndList(f, item, priceList);
        // Ninguna configuración para TenantId → Moq devuelve el default (null) para cualquier
        // combinación no configurada explícitamente, incluida otra tenant — fail-closed por
        // ausencia de match, igual que ForOperationalScope a nivel de repositorio real.
        f.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceListItem?)null);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnitPrice.Should().Be(100m);
        f.Assignments.Verify(
            a => a.FindByKeyAsync(TenantId, priceList.Id, item.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
