using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Modules.Pricing.Services;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// SALES-ITEM-SEARCH-PRICELIST-ASSIGNMENT-AUDIT-03 — hallazgo raíz: el buscador de ítems de
/// Ventas (columna "Promo" de <see cref="InvoiceItemSearchResultDto"/>) reimplementaba la
/// precedencia de reglas llamando <see cref="PricingCalculation"/> directamente, sin pasar nunca
/// por <see cref="IPricingResolver"/> NI por la verificación de asignación agregada en
/// PRICING-LIST-ASSIGNMENT-ENFORCEMENT-02 — mostraba el descuento/recargo general de la lista
/// default a CUALQUIER ítem, estuviera o no asignado (<see cref="PriceListItem"/> activo). Esta
/// suite cubre el mismo criterio ya probado para <c>PricingResolver</c> y
/// <c>GetItemPricingSimulationQueryHandler</c>, aplicado a este tercer call-site.
/// </summary>
public sealed class SalesItemSearchPriceListAssignmentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly DateOnly CompanyToday = new(2026, 9, 19);

    private sealed class Fixture
    {
        public Mock<IInvoiceItemSearchRepository> Repo { get; } = new();
        public Mock<ISriCatalogResolver> Sri { get; } = new();
        public Mock<IPriceListRepository> PriceLists { get; } = new();
        public Mock<IPricingRuleRepository> Rules { get; } = new();
        public Mock<IPriceListItemRepository> Assignments { get; } = new();
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

        public Fixture(decimal basePrice = 100m, string? vatCode = "10", decimal vatPercent = 15m)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            CompanyClock
                .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CompanyToday);

            var match = new InvoiceItemMatch(
                ItemId, "SKU-001", "Item de prueba", null, "UNI", true, "Bodega Principal",
                10m, 5m, basePrice, vatCode, null, "UNI",
                Array.Empty<InvoiceItemPackagingLevelDto>(), null
            );
            Repo.Setup(r =>
                    r.SearchAsync(TenantId, CompanyId, It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(new[] { match });

            Sri.Setup(s => s.ResolveVatRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(vatCode is null
                    ? new Dictionary<string, SriVatInfo>()
                    : new Dictionary<string, SriVatInfo> { [vatCode] = new("IVA", vatPercent) });
            Sri.Setup(s => s.ResolveIceRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, SriIceInfo>());
        }

        public SearchItemsForInvoiceHandler Build() =>
            new(
                Repo.Object,
                Sri.Object,
                PriceLists.Object,
                Rules.Object,
                Assignments.Object,
                Strategies,
                Tenant.Object,
                Company.Object,
                CompanyClock.Object
            );
    }

    private static PriceList CreateDefaultList(PricingRuleType ruleType, decimal ruleValue) =>
        PriceList.Create(
            TenantId, CompanyId, "GENERAL", "Lista General", "USD",
            isDefault: true, createdBy: UserId, ruleType: ruleType, ruleValue: ruleValue
        );

    [Fact]
    public async Task Item_no_asignado_no_muestra_promo_y_precio_final_deriva_del_PVP()
    {
        var f = new Fixture(basePrice: 100m);
        var defaultList = CreateDefaultList(PricingRuleType.PercentDiscount, 5m);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);
        f.Rules.Setup(r => r.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PricingRule>());
        // Sin asignación para ItemId en esta lista.
        f.Assignments.Setup(a => a.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceListItem>());

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value!.Single();
        row.PriceListName.Should().BeNull();
        row.DiscountDescription.Should().BeNull();
        row.DiscountedSalePriceWithoutTax.Should().BeNull();
        row.DiscountedFinalSalePrice.Should().BeNull();
        // Precio final = PVP (100) con IVA 15% = 115, nunca el -5% de la lista.
        row.SalePriceWithoutTax.Should().Be(100m);
        row.FinalSalePrice.Should().Be(115m);
    }

    [Fact]
    public async Task Item_asignado_activo_muestra_la_regla_general_de_la_lista()
    {
        var f = new Fixture(basePrice: 100m);
        var defaultList = CreateDefaultList(PricingRuleType.PercentDiscount, 5m);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);
        f.Rules.Setup(r => r.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PricingRule>());
        f.Assignments.Setup(a => a.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PriceListItem.Create(TenantId, CompanyId, defaultList.Id, ItemId, UserId) });

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value!.Single();
        row.PriceListName.Should().Be("Lista General");
        row.DiscountDescription.Should().Contain("regla general");
        row.DiscountedSalePriceWithoutTax.Should().Be(95m);
        row.DiscountedFinalSalePrice.Should().Be(109.25m);
    }

    [Fact]
    public async Task Item_asignado_con_excepcion_muestra_la_excepcion_no_la_regla_general()
    {
        var f = new Fixture(basePrice: 100m);
        var defaultList = CreateDefaultList(PricingRuleType.PercentDiscount, 5m);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);
        var exception = PricingRule.Create(TenantId, CompanyId, defaultList.Id, ItemId, PricingRuleType.PercentDiscount, 20m, UserId);
        f.Rules.Setup(r => r.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { exception });
        f.Assignments.Setup(a => a.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PriceListItem.Create(TenantId, CompanyId, defaultList.Id, ItemId, UserId) });

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value!.Single();
        // Excepción (20% descuento) gana sobre la regla general (5% descuento) → 80, nunca 95.
        row.DiscountDescription.Should().Contain("excepción");
        row.DiscountedSalePriceWithoutTax.Should().Be(80m);
    }

    [Fact]
    public async Task Asignacion_deshabilitada_no_muestra_promo()
    {
        var f = new Fixture(basePrice: 100m);
        var defaultList = CreateDefaultList(PricingRuleType.PercentDiscount, 5m);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);
        f.Rules.Setup(r => r.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PricingRule>());
        // GetByPriceListAsync real ya filtra IsActive — una asignación deshabilitada nunca aparece
        // en esta colección; se simula ese mismo contrato devolviendo la lista vacía.
        f.Assignments.Setup(a => a.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceListItem>());

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Single().DiscountDescription.Should().BeNull();
    }

    [Fact]
    public async Task El_IVA_se_sigue_calculando_correctamente_con_y_sin_promo()
    {
        var f = new Fixture(basePrice: 100m, vatCode: "10", vatPercent: 15m);
        var defaultList = CreateDefaultList(PricingRuleType.PercentDiscount, 5m);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);
        f.Rules.Setup(r => r.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PricingRule>());
        f.Assignments.Setup(a => a.GetByPriceListAsync(TenantId, defaultList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PriceListItem.Create(TenantId, CompanyId, defaultList.Id, ItemId, UserId) });

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null), CancellationToken.None);

        var row = result.Value!.Single();
        // Precio base 100 con IVA 15% = 115 (sin promo aplicada al campo "regular").
        row.FinalSalePrice.Should().Be(115m);
        // Precio con -5% = 95, con IVA 15% = 109.25.
        row.DiscountedFinalSalePrice.Should().Be(109.25m);
        row.VatDisplay.Should().Be("IVA 15%");
    }
}
