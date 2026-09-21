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
/// PRICING-CONTEXTUAL-RESOLUTION-05C — PricingResolver.ResolveAsync(PricingContext, ct) recorre
/// los candidatos de IPriceListSelectionResolver en orden (Customer → CompanyDefault) y usa el
/// primero cuyo ítem tenga un PriceListItem activo — sin reimplementar PricingCalculation ni la
/// vigencia de listas (eso ya lo resuelve IPriceListSelectionResolver, probado en
/// PriceListSelectionResolverTests). Todavía NO consumido desde Sales.
/// </summary>
public sealed class PricingContextualResolutionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IItemRepository> Items { get; } = new();
        public Mock<IPriceListRepository> PriceLists { get; } = new();
        public Mock<IPricingRuleRepository> Rules { get; } = new();
        public Mock<IPriceListItemRepository> Assignments { get; } = new();
        public Mock<IPriceListSelectionResolver> Selection { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICompanyClock> CompanyClock { get; } = new();

        public IPricingAdjustmentStrategyResolver Strategies { get; } =
            new PricingAdjustmentStrategyResolver(new IPricingAdjustmentStrategy[]
            {
                new PercentDiscountStrategy(),
                new PercentMarkupStrategy(),
                new FixedAdjustmentStrategy(),
                new FixedPriceStrategy(),
            });

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
        }

        public PricingResolver Build() =>
            new(
                Items.Object,
                PriceLists.Object,
                Rules.Object,
                Assignments.Object,
                Selection.Object,
                Strategies,
                Tenant.Object,
                Company.Object,
                CompanyClock.Object,
                PrecisionPolicyTestDouble.Mock()
            );
    }

    private static Item CreateItem(decimal basePrice = 100m) =>
        Item.Create(
            TenantId, "SKU-001", "Item de prueba", "Item de prueba", ItemTypeId, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(), UserId, baseSalePrice: basePrice
        );

    private static PriceList CreateList(
        string code,
        PricingRuleType? ruleType = PricingRuleType.PercentDiscount,
        decimal? ruleValue = 10m
    ) =>
        PriceList.Create(
            TenantId, CompanyId, code, $"Lista {code}", "USD",
            isDefault: code == "DEFAULT", createdBy: UserId, ruleType: ruleType, ruleValue: ruleValue
        );

    private static PriceListSelectionResult Candidate(PriceList list, PriceListSelectionSource source) =>
        new(list.Id, list.Name, source);

    private void SetupItem(Fixture f, Item item) =>
        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

    private void SetupList(Fixture f, PriceList list) =>
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

    private void SetupAssignment(Fixture f, PriceList list, Item item, bool active) =>
        f.Assignments
            .Setup(a => a.FindByKeyAsync(TenantId, list.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active ? PriceListItem.Create(TenantId, CompanyId, list.Id, item.Id, UserId) : null);

    [Fact]
    public async Task Item_asignado_en_la_lista_del_cliente_usa_esa_lista_con_SelectionSource_Customer()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP");
        SetupItem(f, item);
        SetupList(f, customerList);
        SetupAssignment(f, customerList, item, active: true);
        f.Selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Candidate(customerList, PriceListSelectionSource.Customer) });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().Be(customerList.Id);
        result.Value!.SelectionSource.Should().Be(PriceListSelectionSource.Customer);
        result.Value!.UnitPrice.Should().Be(90m);
    }

    [Fact]
    public async Task Item_no_asignado_en_lista_del_cliente_pero_si_en_default_usa_Default()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP");
        var defaultList = CreateList("DEFAULT", PricingRuleType.PercentMarkup, 20m);
        SetupItem(f, item);
        SetupList(f, customerList);
        SetupList(f, defaultList);
        SetupAssignment(f, customerList, item, active: false);
        SetupAssignment(f, defaultList, item, active: true);
        f.Selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Candidate(customerList, PriceListSelectionSource.Customer),
                Candidate(defaultList, PriceListSelectionSource.CompanyDefault),
            });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().Be(defaultList.Id);
        result.Value!.SelectionSource.Should().Be(PriceListSelectionSource.CompanyDefault);
        result.Value!.UnitPrice.Should().Be(120m);
    }

    [Fact]
    public async Task Item_no_asignado_en_ningun_candidato_usa_PVP()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP");
        var defaultList = CreateList("DEFAULT");
        SetupItem(f, item);
        SetupList(f, customerList);
        SetupList(f, defaultList);
        SetupAssignment(f, customerList, item, active: false);
        SetupAssignment(f, defaultList, item, active: false);
        f.Selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Candidate(customerList, PriceListSelectionSource.Customer),
                Candidate(defaultList, PriceListSelectionSource.CompanyDefault),
            });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().BeNull();
        result.Value!.PriceListCode.Should().Be("PVP");
        result.Value!.SelectionSource.Should().BeNull();
        result.Value!.UnitPrice.Should().Be(100m);
    }

    [Fact]
    public async Task Lista_del_cliente_invalida_selection_resolver_ya_no_la_ofrece_como_candidata_usa_Default()
    {
        // IPriceListSelectionResolver ya filtra vigencia/activación (probado en
        // PriceListSelectionResolverTests) — una lista de cliente inválida simplemente no
        // aparece en la colección de candidatos que llega aquí.
        var f = new Fixture();
        var item = CreateItem(100m);
        var defaultList = CreateList("DEFAULT");
        SetupItem(f, item);
        SetupList(f, defaultList);
        SetupAssignment(f, defaultList, item, active: true);
        f.Selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Candidate(defaultList, PriceListSelectionSource.CompanyDefault) });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().Be(defaultList.Id);
        result.Value!.SelectionSource.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Excepcion_del_cliente_gana_sobre_la_regla_general_de_su_propia_lista()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP", PricingRuleType.PercentMarkup, 20m);
        SetupItem(f, item);
        SetupList(f, customerList);
        SetupAssignment(f, customerList, item, active: true);
        var exception = PricingRule.Create(TenantId, CompanyId, customerList.Id, item.Id, PricingRuleType.PercentDiscount, 15m, UserId);
        f.Rules
            .Setup(r => r.GetActiveForItemInListAsync(TenantId, customerList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exception);
        f.Selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Candidate(customerList, PriceListSelectionSource.Customer) });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnitPrice.Should().Be(85m);
        result.Value!.RuleApplied.Should().Contain("ítem");
        result.Value!.SelectionSource.Should().Be(PriceListSelectionSource.Customer);
    }

    [Fact]
    public async Task Excepcion_en_la_lista_default_gana_sobre_su_regla_general()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var defaultList = CreateList("DEFAULT", PricingRuleType.PercentMarkup, 20m);
        SetupItem(f, item);
        SetupList(f, defaultList);
        SetupAssignment(f, defaultList, item, active: true);
        var exception = PricingRule.Create(TenantId, CompanyId, defaultList.Id, item.Id, PricingRuleType.FixedPrice, 77m, UserId);
        f.Rules
            .Setup(r => r.GetActiveForItemInListAsync(TenantId, defaultList.Id, item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exception);
        f.Selection
            .Setup(s => s.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Candidate(defaultList, PriceListSelectionSource.CompanyDefault) });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnitPrice.Should().Be(77m);
        result.Value!.SelectionSource.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Asignacion_deshabilitada_en_el_primer_candidato_pasa_al_siguiente()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP");
        var defaultList = CreateList("DEFAULT");
        SetupItem(f, item);
        SetupList(f, customerList);
        SetupList(f, defaultList);
        SetupAssignment(f, customerList, item, active: false);
        SetupAssignment(f, defaultList, item, active: true);
        f.Selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Candidate(customerList, PriceListSelectionSource.Customer),
                Candidate(defaultList, PriceListSelectionSource.CompanyDefault),
            });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.SelectionSource.Should().Be(PriceListSelectionSource.CompanyDefault);
        f.Assignments.Verify(
            a => a.FindByKeyAsync(TenantId, customerList.Id, item.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.Assignments.Verify(
            a => a.FindByKeyAsync(TenantId, defaultList.Id, item.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Cuando_cliente_y_default_son_la_misma_lista_solo_se_evalua_una_vez()
    {
        // El único candidato posible es la lista compartida (IPriceListSelectionResolver ya
        // deduplica, ver PriceListSelectionResolverTests) — GetByIdAsync/FindByKeyAsync se llaman
        // exactamente una vez, nunca dos.
        var f = new Fixture();
        var item = CreateItem(100m);
        var sharedList = CreateList("DEFAULT");
        SetupItem(f, item);
        SetupList(f, sharedList);
        SetupAssignment(f, sharedList, item, active: true);
        f.Selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Candidate(sharedList, PriceListSelectionSource.Customer) });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.PriceLists.Verify(
            r => r.GetByIdAsync(TenantId, sharedList.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.Assignments.Verify(
            a => a.FindByKeyAsync(TenantId, sharedList.Id, item.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Sin_CustomerId_propaga_null_al_selection_resolver_nunca_Guid_Empty()
    {
        // PRICING-CONTEXT-NULL-CUSTOMER-05C1: null significa explícitamente "sin cliente" — se
        // propaga tal cual a IPriceListSelectionResolver, sin sustituirlo por Guid.Empty como
        // sentinel mágico.
        var f = new Fixture();
        var item = CreateItem(100m);
        var defaultList = CreateList("DEFAULT");
        SetupItem(f, item);
        SetupList(f, defaultList);
        SetupAssignment(f, defaultList, item, active: true);
        f.Selection
            .Setup(s => s.ResolveAsync((Guid?)null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Candidate(defaultList, PriceListSelectionSource.CompanyDefault) });

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().Be(defaultList.Id);
        result.Value!.SelectionSource.Should().Be(PriceListSelectionSource.CompanyDefault);
        f.Selection.Verify(s => s.ResolveAsync((Guid?)null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_ninguna_lista_candidata_usa_PVP()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        SetupItem(f, item);
        f.Selection
            .Setup(s => s.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceListSelectionResult>());

        var result = await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PriceListId.Should().BeNull();
        result.Value!.PriceListCode.Should().Be("PVP");
        result.Value!.UnitPrice.Should().Be(100m);
        f.PriceLists.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Usa_el_TenantId_ambiental_en_todas_las_consultas_fail_closed()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var defaultList = CreateList("DEFAULT");
        SetupItem(f, item);
        SetupList(f, defaultList);
        SetupAssignment(f, defaultList, item, active: true);
        f.Selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Candidate(defaultList, PriceListSelectionSource.CompanyDefault) });

        await f.Build().ResolveAsync(new PricingContext(item.Id, CustomerId), CancellationToken.None);

        f.Items.Verify(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()), Times.Once);
        f.PriceLists.Verify(r => r.GetByIdAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()), Times.Once);
        f.Assignments.Verify(
            a => a.FindByKeyAsync(TenantId, defaultList.Id, item.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.Rules.Verify(
            r => r.GetActiveForItemInListAsync(TenantId, defaultList.Id, item.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
