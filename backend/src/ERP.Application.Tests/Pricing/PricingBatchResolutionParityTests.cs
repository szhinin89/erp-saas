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
/// PRICING-CONTEXTUAL-BATCH-RESOLUTION-05D — ResolveManyAsync debe producir, para el mismo
/// contexto comercial, EXACTAMENTE el mismo PricingResult por ítem que ResolveAsync(PricingContext,
/// ct) — un solo motor de decisión (BuildResultForList), nunca dos implementaciones que puedan
/// divergir. También cubre que las consultas por lista (PriceList/PriceListItem/PricingRule) se
/// hacen en batch — nunca una por ítem — verificando conteos exactos de invocación.
/// </summary>
public sealed class PricingBatchResolutionParityTests
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

        // Estado mutable por lista — permite registrar asignaciones/reglas incrementalmente y
        // que tanto los mocks "por clave" (single) como los "por lista" (batch) lean siempre el
        // estado actual, sin duplicar la configuración de cada escenario.
        private readonly Dictionary<Guid, List<PriceListItem>> _assignmentsByList = new();
        private readonly Dictionary<Guid, List<PricingRule>> _rulesByList = new();

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

        public void RegisterItem(Item item) =>
            Items.Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

        public void RegisterItemsBatch(params Item[] items)
        {
            foreach (var item in items)
                RegisterItem(item);
            Items
                .Setup(r => r.GetByIdsLightAsync(
                    It.Is<IReadOnlyCollection<Guid>>(ids => items.All(i => ids.Contains(i.Id))),
                    TenantId,
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(items);
        }

        public void RegisterList(PriceList list)
        {
            PriceLists.Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);
            if (!_assignmentsByList.ContainsKey(list.Id))
            {
                _assignmentsByList[list.Id] = new List<PriceListItem>();
                Assignments
                    .Setup(a => a.GetByPriceListAsync(TenantId, list.Id, It.IsAny<CancellationToken>()))
                    .Returns(() => Task.FromResult<IReadOnlyList<PriceListItem>>(_assignmentsByList[list.Id]));
            }
            if (!_rulesByList.ContainsKey(list.Id))
            {
                _rulesByList[list.Id] = new List<PricingRule>();
                Rules
                    .Setup(r => r.GetByPriceListAsync(TenantId, list.Id, It.IsAny<CancellationToken>()))
                    .Returns(() => Task.FromResult<IReadOnlyList<PricingRule>>(_rulesByList[list.Id]));
            }
        }

        public void AssignActive(PriceList list, Item item)
        {
            var assignment = PriceListItem.Create(TenantId, CompanyId, list.Id, item.Id, UserId);
            _assignmentsByList[list.Id].Add(assignment);
            Assignments
                .Setup(a => a.FindByKeyAsync(TenantId, list.Id, item.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(assignment);
        }

        public void AssignDisabled(PriceList list, Item item)
        {
            var assignment = PriceListItem.Create(TenantId, CompanyId, list.Id, item.Id, UserId);
            assignment.Disable(UserId);
            // Deshabilitada: nunca aparece en GetByPriceListAsync (ya filtra IsActive) — solo se
            // registra el FindByKeyAsync puntual que sí la devuelve (para el resolver single).
            Assignments
                .Setup(a => a.FindByKeyAsync(TenantId, list.Id, item.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(assignment);
        }

        public void SetException(PriceList list, Item item, PricingRuleType type, decimal value)
        {
            var rule = PricingRule.Create(TenantId, CompanyId, list.Id, item.Id, type, value, UserId);
            _rulesByList[list.Id].Add(rule);
            Rules
                .Setup(r => r.GetActiveForItemInListAsync(TenantId, list.Id, item.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rule);
        }

        public void SetCandidates(params PriceListSelectionResult[] candidates) =>
            Selection
                .Setup(s => s.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(candidates);
    }

    private static Item CreateItem(decimal basePrice = 100m) =>
        Item.Create(
            TenantId, $"SKU-{Guid.NewGuid():N}"[..12], "Item de prueba", "Item de prueba", ItemTypeId, "UNIT",
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

    private async Task AssertSingleAndBatchMatchAsync(Fixture f, Item item, Guid? customerId)
    {
        var single = await f.Build().ResolveAsync(new PricingContext(item.Id, customerId), CancellationToken.None);
        var batch = await f.Build().ResolveManyAsync(
            new PricingBatchContext(new[] { item.Id }, customerId),
            CancellationToken.None
        );

        single.IsSuccess.Should().BeTrue(single.Error);
        batch.IsSuccess.Should().BeTrue(batch.Error);
        batch.Value!.Should().ContainKey(item.Id);
        batch.Value![item.Id].Should().BeEquivalentTo(single.Value);
    }

    [Fact]
    public async Task Paridad_Customer_item_asignado_en_lista_del_cliente()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP");
        f.RegisterItem(item);
        f.RegisterItemsBatch(item);
        f.RegisterList(customerList);
        f.AssignActive(customerList, item);
        f.SetCandidates(Candidate(customerList, PriceListSelectionSource.Customer));

        await AssertSingleAndBatchMatchAsync(f, item, CustomerId);
    }

    [Fact]
    public async Task Paridad_fallback_Default_cuando_cliente_no_tiene_lista()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var defaultList = CreateList("DEFAULT", PricingRuleType.PercentMarkup, 20m);
        f.RegisterItem(item);
        f.RegisterItemsBatch(item);
        f.RegisterList(defaultList);
        f.AssignActive(defaultList, item);
        f.SetCandidates(Candidate(defaultList, PriceListSelectionSource.CompanyDefault));

        await AssertSingleAndBatchMatchAsync(f, item, CustomerId);
    }

    [Fact]
    public async Task Paridad_PVP_cuando_ningun_candidato_tiene_el_item_asignado()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP");
        var defaultList = CreateList("DEFAULT");
        f.RegisterItem(item);
        f.RegisterItemsBatch(item);
        f.RegisterList(customerList);
        f.RegisterList(defaultList);
        // Ningún AssignActive — el ítem no está asignado en ninguna lista.
        f.SetCandidates(
            Candidate(customerList, PriceListSelectionSource.Customer),
            Candidate(defaultList, PriceListSelectionSource.CompanyDefault)
        );

        await AssertSingleAndBatchMatchAsync(f, item, CustomerId);
    }

    [Fact]
    public async Task Paridad_excepcion_gana_sobre_regla_general()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP", PricingRuleType.PercentMarkup, 20m);
        f.RegisterItem(item);
        f.RegisterItemsBatch(item);
        f.RegisterList(customerList);
        f.AssignActive(customerList, item);
        f.SetException(customerList, item, PricingRuleType.PercentDiscount, 15m);
        f.SetCandidates(Candidate(customerList, PriceListSelectionSource.Customer));

        await AssertSingleAndBatchMatchAsync(f, item, CustomerId);
    }

    [Fact]
    public async Task Paridad_assignment_disabled_pasa_al_siguiente_candidato()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP");
        var defaultList = CreateList("DEFAULT");
        f.RegisterItem(item);
        f.RegisterItemsBatch(item);
        f.RegisterList(customerList);
        f.RegisterList(defaultList);
        f.AssignDisabled(customerList, item);
        f.AssignActive(defaultList, item);
        f.SetCandidates(
            Candidate(customerList, PriceListSelectionSource.Customer),
            Candidate(defaultList, PriceListSelectionSource.CompanyDefault)
        );

        await AssertSingleAndBatchMatchAsync(f, item, CustomerId);
    }

    [Fact]
    public async Task Paridad_item_asignado_solo_en_la_segunda_lista()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var customerList = CreateList("VIP");
        var defaultList = CreateList("DEFAULT", PricingRuleType.FixedAdjustment, -5m);
        f.RegisterItem(item);
        f.RegisterItemsBatch(item);
        f.RegisterList(customerList);
        f.RegisterList(defaultList);
        // customerList existe como candidato pero el ítem nunca fue asignado ahí — solo en la
        // segunda (defaultList).
        f.AssignActive(defaultList, item);
        f.SetCandidates(
            Candidate(customerList, PriceListSelectionSource.Customer),
            Candidate(defaultList, PriceListSelectionSource.CompanyDefault)
        );

        await AssertSingleAndBatchMatchAsync(f, item, CustomerId);
    }

    [Fact]
    public async Task Paridad_sin_CustomerId()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var defaultList = CreateList("DEFAULT");
        f.RegisterItem(item);
        f.RegisterItemsBatch(item);
        f.RegisterList(defaultList);
        f.AssignActive(defaultList, item);
        f.SetCandidates(Candidate(defaultList, PriceListSelectionSource.CompanyDefault));

        await AssertSingleAndBatchMatchAsync(f, item, null);
    }

    [Fact]
    public async Task Multiples_items_con_resultados_distintos_en_un_solo_batch()
    {
        var f = new Fixture();
        var itemInCustomerList = CreateItem(100m);
        var itemOnlyInDefault = CreateItem(200m);
        var itemUnassigned = CreateItem(50m);
        var customerList = CreateList("VIP", PricingRuleType.PercentDiscount, 10m);
        var defaultList = CreateList("DEFAULT", PricingRuleType.PercentMarkup, 20m);
        f.RegisterItemsBatch(itemInCustomerList, itemOnlyInDefault, itemUnassigned);
        f.RegisterList(customerList);
        f.RegisterList(defaultList);
        f.AssignActive(customerList, itemInCustomerList);
        f.AssignActive(defaultList, itemOnlyInDefault);
        // itemUnassigned: sin asignación en ninguna lista → PVP.
        f.SetCandidates(
            Candidate(customerList, PriceListSelectionSource.Customer),
            Candidate(defaultList, PriceListSelectionSource.CompanyDefault)
        );

        var batch = await f.Build().ResolveManyAsync(
            new PricingBatchContext(
                new[] { itemInCustomerList.Id, itemOnlyInDefault.Id, itemUnassigned.Id },
                CustomerId
            ),
            CancellationToken.None
        );

        batch.IsSuccess.Should().BeTrue(batch.Error);
        batch.Value!.Should().HaveCount(3);
        batch.Value![itemInCustomerList.Id].UnitPrice.Should().Be(90m);
        batch.Value![itemInCustomerList.Id].SelectionSource.Should().Be(PriceListSelectionSource.Customer);
        batch.Value![itemOnlyInDefault.Id].UnitPrice.Should().Be(240m);
        batch.Value![itemOnlyInDefault.Id].SelectionSource.Should().Be(PriceListSelectionSource.CompanyDefault);
        batch.Value![itemUnassigned.Id].PriceListId.Should().BeNull();
        batch.Value![itemUnassigned.Id].UnitPrice.Should().Be(50m);

        // Nunca N+1: las listas/asignaciones/reglas se consultan por CANDIDATO, no por ítem —
        // con 3 ítems y 2 candidatos, cada método de batch se llama exactamente 2 veces (una por
        // lista), nunca 6 (3 ítems × 2 listas) ni más.
        f.Assignments.Verify(
            a => a.GetByPriceListAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        f.Rules.Verify(
            r => r.GetByPriceListAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        f.PriceLists.Verify(
            r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        f.Items.Verify(
            r => r.GetByIdsLightAsync(It.IsAny<IReadOnlyCollection<Guid>>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.Selection.Verify(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Batch_usa_el_TenantId_ambiental_en_todas_las_consultas_fail_closed()
    {
        var f = new Fixture();
        var item = CreateItem(100m);
        var defaultList = CreateList("DEFAULT");
        f.RegisterItemsBatch(item);
        f.RegisterList(defaultList);
        f.AssignActive(defaultList, item);
        f.SetCandidates(Candidate(defaultList, PriceListSelectionSource.CompanyDefault));

        await f.Build().ResolveManyAsync(new PricingBatchContext(new[] { item.Id }, CustomerId), CancellationToken.None);

        f.Items.Verify(
            r => r.GetByIdsLightAsync(It.IsAny<IReadOnlyCollection<Guid>>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.PriceLists.Verify(r => r.GetByIdAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()), Times.Once);
        f.Assignments.Verify(
            a => a.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.Rules.Verify(
            r => r.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Batch_vacio_no_hace_ninguna_consulta_y_devuelve_diccionario_vacio()
    {
        var f = new Fixture();

        var result = await f.Build().ResolveManyAsync(
            new PricingBatchContext(Array.Empty<Guid>(), CustomerId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Should().BeEmpty();
        f.Selection.Verify(
            s => s.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
