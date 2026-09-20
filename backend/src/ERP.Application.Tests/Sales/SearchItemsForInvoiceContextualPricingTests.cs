using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.UseCases;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// SALES-CONTEXTUAL-PRICING-READ-06A — SearchItemsForInvoiceHandler ya NO resuelve PriceList
/// default, PriceListItem ni PricingRule por su cuenta (eso vivía aquí desde SALES-ITEM-SEARCH-
/// PRICELIST-ASSIGNMENT-AUDIT-03 / PRICING-LIST-ASSIGNMENT-ENFORCEMENT-02): delega TODO a una
/// única llamada batch a IPricingResolver.ResolveManyAsync y solo enriquece el DTO con el
/// PricingResult ya resuelto. Esta suite reemplaza a SalesItemSearchPriceListAssignmentTests.cs
/// (obsoleta: mockeaba repos que el handler ya no tiene) e InvoiceItemSearchCompanyClockTests.cs
/// (obsoleta: la vigencia de listas ahora es responsabilidad exclusiva de Pricing, ya probada en
/// PriceListSelectionResolverTests).
/// </summary>
public sealed class SearchItemsForInvoiceContextualPricingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IInvoiceItemSearchRepository> Repo { get; } = new();
        public Mock<ISriCatalogResolver> Sri { get; } = new();
        public Mock<IPricingResolver> PricingResolver { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();

        public Fixture(decimal basePrice = 100m, string? vatCode = "10", decimal vatPercent = 15m)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);

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
            new(Repo.Object, Sri.Object, PricingResolver.Object, Tenant.Object, Company.Object);

        public void SetPricing(Guid? customerId, PricingResult result) =>
            PricingResolver
                .Setup(p => p.ResolveManyAsync(
                    It.Is<PricingBatchContext>(c => c.ItemIds.Contains(ItemId) && c.CustomerId == customerId),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(Result<IReadOnlyDictionary<Guid, PricingResult>>.Success(
                    new Dictionary<Guid, PricingResult> { [ItemId] = result }
                ));
    }

    private static PricingResult CustomerResult(decimal unitPrice, string ruleApplied = "PercentDiscount:10") =>
        new(ItemId, Guid.NewGuid(), "VIP", "Lista VIP", "USD", 100m, ruleApplied, unitPrice,
            "Descuento 10% (regla general)", PriceListSelectionSource.Customer);

    private static PricingResult DefaultResult(decimal unitPrice, string ruleApplied = "PercentMarkup:20") =>
        new(ItemId, Guid.NewGuid(), "GEN", "Lista General", "USD", 100m, ruleApplied, unitPrice,
            "Recargo 20% (regla general)", PriceListSelectionSource.CompanyDefault);

    private static PricingResult PvpResult(decimal basePrice = 100m) =>
        new(ItemId, null, "PVP", "Precio base", "USD", basePrice, null, basePrice, null, null);

    [Fact]
    public async Task Cliente_con_lista_y_item_asignado_muestra_promo_de_la_lista_del_cliente()
    {
        var f = new Fixture(basePrice: 100m);
        f.SetPricing(CustomerId, CustomerResult(90m));

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value!.Single();
        row.PriceListName.Should().Be("Lista VIP");
        row.DiscountDescription.Should().Be("Descuento 10% (regla general)");
        row.DiscountedSalePriceWithoutTax.Should().Be(90m);
        row.DiscountedFinalSalePrice.Should().Be(103.5m);
    }

    [Fact]
    public async Task Item_no_asignado_en_lista_del_cliente_pero_si_en_default_muestra_promo_default()
    {
        var f = new Fixture(basePrice: 100m);
        f.SetPricing(CustomerId, DefaultResult(120m));

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value!.Single();
        row.PriceListName.Should().Be("Lista General");
        row.DiscountedSalePriceWithoutTax.Should().Be(120m);
    }

    [Fact]
    public async Task Item_no_asignado_en_ninguna_lista_no_muestra_promo_precio_final_es_PVP()
    {
        var f = new Fixture(basePrice: 100m);
        f.SetPricing(CustomerId, PvpResult(100m));

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value!.Single();
        row.PriceListName.Should().BeNull();
        row.DiscountDescription.Should().BeNull();
        row.DiscountedSalePriceWithoutTax.Should().BeNull();
        row.DiscountedFinalSalePrice.Should().BeNull();
        row.SalePriceWithoutTax.Should().Be(100m);
        row.FinalSalePrice.Should().Be(115m);
    }

    [Fact]
    public async Task Excepcion_del_cliente_se_refleja_como_promo()
    {
        var f = new Fixture(basePrice: 100m);
        f.SetPricing(CustomerId, CustomerResult(80m, "PercentDiscount:20 (ítem)"));

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Single().DiscountedSalePriceWithoutTax.Should().Be(80m);
    }

    [Fact]
    public async Task Cliente_sin_lista_propia_usa_default()
    {
        var f = new Fixture(basePrice: 100m);
        // El resolver ya decide "sin lista de cliente → default" — el handler solo consume el
        // resultado, cualquiera sea el origen.
        f.SetPricing(CustomerId, DefaultResult(110m));

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Single().PriceListName.Should().Be("Lista General");
    }

    [Fact]
    public async Task CustomerId_null_usa_default()
    {
        var f = new Fixture(basePrice: 100m);
        f.SetPricing(null, DefaultResult(105m));

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Single().PriceListName.Should().Be("Lista General");
        f.PricingResolver.Verify(
            p => p.ResolveManyAsync(
                It.Is<PricingBatchContext>(c => c.CustomerId == null),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Cambiar_customerId_entre_busquedas_devuelve_precios_distintos()
    {
        var f = new Fixture(basePrice: 100m);
        var otherCustomerId = Guid.NewGuid();
        f.SetPricing(CustomerId, CustomerResult(90m));
        f.SetPricing(otherCustomerId, PvpResult(100m));

        var first = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);
        var second = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: otherCustomerId), CancellationToken.None);

        first.Value!.Single().DiscountedSalePriceWithoutTax.Should().Be(90m);
        second.Value!.Single().DiscountedSalePriceWithoutTax.Should().BeNull();
        second.Value!.Single().SalePriceWithoutTax.Should().Be(100m);
    }

    [Fact]
    public async Task Llama_ResolveManyAsync_exactamente_una_vez_nunca_por_item()
    {
        var f = new Fixture(basePrice: 100m);
        f.SetPricing(CustomerId, CustomerResult(90m));

        await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);

        f.PricingResolver.Verify(
            p => p.ResolveManyAsync(It.IsAny<PricingBatchContext>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.PricingResolver.Verify(
            p => p.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.PricingResolver.Verify(
            p => p.ResolveAsync(It.IsAny<PricingContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task El_IVA_se_sigue_calculando_correctamente_con_y_sin_promo()
    {
        var f = new Fixture(basePrice: 100m, vatCode: "10", vatPercent: 15m);
        f.SetPricing(CustomerId, CustomerResult(90m));

        var result = await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);

        var row = result.Value!.Single();
        row.FinalSalePrice.Should().Be(115m);
        row.DiscountedFinalSalePrice.Should().Be(103.5m);
        row.VatDisplay.Should().Be("IVA 15%");
    }

    [Fact]
    public async Task Usa_el_TenantId_y_CompanyId_ambientales_fail_closed()
    {
        var f = new Fixture(basePrice: 100m);
        f.SetPricing(CustomerId, CustomerResult(90m));

        await f.Build().Handle(new SearchItemsForInvoiceQuery("prue", null, CustomerId: CustomerId), CancellationToken.None);

        f.Repo.Verify(
            r => r.SearchAsync(TenantId, CompanyId, It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
