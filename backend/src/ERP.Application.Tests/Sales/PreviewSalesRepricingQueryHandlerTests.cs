using ERP.Application.Tests.TestSupport;
using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.UseCases.PreviewSalesRepricing;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// SALES-CUSTOMER-REPRICING-PREVIEW-06C1: PreviewSalesRepricingQueryHandler es SOLO lectura —
/// compone dos resoluciones ya hechas por IPricingResolver.ResolveManyAsync (una por cliente,
/// nunca por ítem) y compara. No reimplementa ninguna regla de pricing, no toca
/// SalesLineBuilder/Draft/Authorization ni PricingCalculation.
/// </summary>
public sealed class PreviewSalesRepricingQueryHandlerTests
{
    private static readonly Guid OldCustomerId = Guid.NewGuid();
    private static readonly Guid NewCustomerId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPricingResolver> Pricing { get; } = new();

        public PreviewSalesRepricingQueryHandler BuildHandler() => new(Pricing.Object, PrecisionPolicyTestDouble.Mock());

        public void SetupOld(Guid? customerId, IReadOnlyDictionary<Guid, PricingResult> dict) =>
            Pricing
                .Setup(p =>
                    p.ResolveManyAsync(
                        It.Is<PricingBatchContext>(c => c.CustomerId == customerId),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<IReadOnlyDictionary<Guid, PricingResult>>.Success(dict));
    }

    private static PricingResult Result_(
        Guid itemId,
        Guid? priceListId,
        string priceListName,
        decimal unitPrice,
        decimal basePrice,
        string? ruleApplied = null,
        string? ruleDescription = null,
        PriceListSelectionSource? source = null
    ) =>
        new(
            itemId,
            priceListId,
            priceListId.HasValue ? "COD" : "PVP",
            priceListName,
            "USD",
            basePrice,
            ruleApplied,
            unitPrice,
            ruleDescription,
            source
        );

    private static PreviewSalesRepricingQuery Query(Guid? oldCustomer, Guid? newCustomer) =>
        new(new List<Guid> { ItemId }, oldCustomer, newCustomer);

    [Fact]
    public async Task Mismo_precio_marca_Changed_false()
    {
        var f = new Fixture();
        var priceListId = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, priceListId, "Lista", 50m, 50m),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, priceListId, "Lista", 50m, 50m),
            }
        );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.Changed.Should().BeFalse();
        item.OldResolvedPrice.Should().Be(50m);
        item.NewResolvedPrice.Should().Be(50m);
    }

    [Fact]
    public async Task Customer_a_Customer_con_precio_distinto_marca_Changed_true()
    {
        var f = new Fixture();
        var listA = Guid.NewGuid();
        var listB = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, listA, "Lista A", 90m, 100m, source: PriceListSelectionSource.Customer),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, listB, "Lista B", 70m, 100m, source: PriceListSelectionSource.Customer),
            }
        );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.Changed.Should().BeTrue();
        item.OldResolvedPrice.Should().Be(90m);
        item.NewResolvedPrice.Should().Be(70m);
        item.OldPriceListId.Should().Be(listA);
        item.NewPriceListId.Should().Be(listB);
        item.OldSelectionSource.Should().Be(PriceListSelectionSource.Customer);
        item.NewSelectionSource.Should().Be(PriceListSelectionSource.Customer);
        // SALES-CUSTOMER-REPRICE-METADATA-06C2A: NewBasePrice = PricingResult.BasePrice del
        // cliente nuevo tal cual, sin recalcular.
        item.NewBasePrice.Should().Be(100m);
    }

    [Fact]
    public async Task Customer_a_Default_refleja_el_cambio_de_lista_y_origen()
    {
        var f = new Fixture();
        var customerListId = Guid.NewGuid();
        var defaultListId = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(
                    ItemId, customerListId, "Lista Mayorista", 90m, 100m,
                    source: PriceListSelectionSource.Customer
                ),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(
                    ItemId, defaultListId, "Lista General", 100m, 100m,
                    source: PriceListSelectionSource.CompanyDefault
                ),
            }
        );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.Changed.Should().BeTrue();
        item.OldSelectionSource.Should().Be(PriceListSelectionSource.Customer);
        item.NewSelectionSource.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Default_a_Customer_refleja_el_cambio_de_lista_y_origen()
    {
        var f = new Fixture();
        var defaultListId = Guid.NewGuid();
        var customerListId = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(
                    ItemId, defaultListId, "Lista General", 100m, 100m,
                    source: PriceListSelectionSource.CompanyDefault
                ),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(
                    ItemId, customerListId, "Lista Mayorista", 85m, 100m,
                    source: PriceListSelectionSource.Customer
                ),
            }
        );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.Changed.Should().BeTrue();
        item.OldSelectionSource.Should().Be(PriceListSelectionSource.CompanyDefault);
        item.NewSelectionSource.Should().Be(PriceListSelectionSource.Customer);
    }

    [Fact]
    public async Task Item_asignado_solo_en_la_segunda_lista_pasa_de_PVP_a_lista_especifica()
    {
        var f = new Fixture();
        var newListId = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, null, "Precio de venta al público", 40m, 40m),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(
                    ItemId, newListId, "Lista Mayorista", 36m, 40m,
                    ruleApplied: "PriceListItem (lista)", source: PriceListSelectionSource.Customer
                ),
            }
        );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.OldPriceListId.Should().BeNull();
        item.NewPriceListId.Should().Be(newListId);
        item.Changed.Should().BeTrue();
    }

    [Fact]
    public async Task PVP_a_lista_marca_Changed_cuando_el_precio_cambia()
    {
        var f = new Fixture();
        var listId = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, null, "Precio de venta al público", 25m, 25m),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, listId, "Lista Mayorista", 20m, 25m, ruleApplied: "PercentDiscount:20"),
            }
        );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.OldPriceListId.Should().BeNull();
        item.NewPriceListId.Should().Be(listId);
        item.Changed.Should().BeTrue();
        // Regla general con descuento real (20/25=0.8): NewBasePrice=25 se conserva íntegro,
        // independiente de que NewResolvedPrice (20) ya venga descontado.
        item.NewBasePrice.Should().Be(25m);
    }

    [Fact]
    public async Task Lista_a_PVP_marca_Changed_cuando_el_precio_cambia()
    {
        var f = new Fixture();
        var listId = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, listId, "Lista Mayorista", 20m, 25m, ruleApplied: "PercentDiscount:20"),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, null, "Precio de venta al público", 25m, 25m),
            }
        );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.OldPriceListId.Should().Be(listId);
        item.NewPriceListId.Should().BeNull();
        item.Changed.Should().BeTrue();
        // Sin descuento (PVP): NewResolvedPrice == NewBasePrice, ambos 25.
        item.NewBasePrice.Should().Be(25m);
        item.NewResolvedPrice.Should().Be(item.NewBasePrice);
    }

    [Fact]
    public async Task Excepcion_de_cliente_se_refleja_en_NewDiscountDescription()
    {
        var f = new Fixture();
        var listId = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, listId, "Lista Mayorista", 90m, 100m, ruleApplied: "PriceListItem (lista)"),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(
                    ItemId, listId, "Lista Mayorista", 70m, 100m,
                    ruleApplied: "PriceListItemException (excepción cliente)",
                    ruleDescription: "Precio de excepción para este cliente"
                ),
            }
        );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.Changed.Should().BeTrue();
        item.NewDiscountDescription.Should().Be("Precio de excepción para este cliente");
        // La excepción de cliente sigue siendo una regla sobre la misma lista/BasePrice — el
        // BasePrice de 100 se conserva igual que en la regla general.
        item.NewBasePrice.Should().Be(100m);
    }

    [Fact]
    public async Task Varios_items_ejecutan_exactamente_2_llamadas_batch()
    {
        var f = new Fixture();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var itemC = Guid.NewGuid();
        f.SetupOld(
            OldCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [itemA] = Result_(itemA, null, "PVP", 10m, 10m),
                [itemB] = Result_(itemB, null, "PVP", 20m, 20m),
                [itemC] = Result_(itemC, null, "PVP", 30m, 30m),
            }
        );
        f.SetupOld(
            NewCustomerId,
            new Dictionary<Guid, PricingResult>
            {
                [itemA] = Result_(itemA, null, "PVP", 10m, 10m),
                [itemB] = Result_(itemB, null, "PVP", 18m, 20m),
                [itemC] = Result_(itemC, null, "PVP", 30m, 30m),
            }
        );

        var query = new PreviewSalesRepricingQuery(
            new List<Guid> { itemA, itemB, itemC },
            OldCustomerId,
            NewCustomerId
        );
        var result = await f.BuildHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Should().HaveCount(3);
        result.Value!.Single(i => i.ItemId == itemB).Changed.Should().BeTrue();
        result.Value!.Where(i => i.ItemId != itemB).Should().OnlyContain(i => !i.Changed);

        f.Pricing.Verify(
            p => p.ResolveManyAsync(It.IsAny<PricingBatchContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        f.Pricing.Verify(
            p => p.ResolveManyAsync(
                It.Is<PricingBatchContext>(c =>
                    c.ItemIds.Count == 3
                    && c.ItemIds.Contains(itemA)
                    && c.ItemIds.Contains(itemB)
                    && c.ItemIds.Contains(itemC)
                    && c.CustomerId == OldCustomerId
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
        f.Pricing.Verify(
            p => p.ResolveManyAsync(
                It.Is<PricingBatchContext>(c => c.ItemIds.Count == 3 && c.CustomerId == NewCustomerId),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CustomerId_null_en_ambos_lados_resuelve_contra_Default_sin_error()
    {
        // "Cliente inválido" según las reglas actuales de IPricingResolver no es un CustomerId
        // que no exista (eso lo decide PricingResolver, no este handler) sino la ausencia de
        // cliente — PricingBatchContext ya soporta CustomerId null (solo candidato
        // CompanyDefault), comportamiento idéntico al de Sales antes de tener cliente.
        var f = new Fixture();
        f.SetupOld(
            null,
            new Dictionary<Guid, PricingResult>
            {
                [ItemId] = Result_(ItemId, null, "PVP", 15m, 15m),
            }
        );

        var result = await f.BuildHandler().Handle(Query(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var item = result.Value!.Single();
        item.Changed.Should().BeFalse();
        f.Pricing.Verify(
            p => p.ResolveManyAsync(
                It.Is<PricingBatchContext>(c => c.CustomerId == null),
                It.IsAny<CancellationToken>()
            ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task Tenant_o_Company_fail_closed_propaga_el_error_sin_devolver_datos()
    {
        // El fail-closed de Tenant/Company vive dentro de IPricingResolver (ICurrentTenant/
        // ICurrentCompany ambientales) — este handler no lo reimplementa, solo debe propagar el
        // Failure tal cual, sin construir ningún item de comparación con datos parciales.
        var f = new Fixture();
        f.Pricing
            .Setup(p => p.ResolveManyAsync(It.IsAny<PricingBatchContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyDictionary<Guid, PricingResult>>.Failure(
                    "No se pudo resolver el contexto de tenant/empresa."
                )
            );

        var result = await f.BuildHandler().Handle(Query(OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Pricing.Verify(
            p => p.ResolveManyAsync(It.IsAny<PricingBatchContext>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Sin_items_devuelve_lista_vacia_sin_llamar_al_resolver()
    {
        var f = new Fixture();

        var result = await f.BuildHandler()
            .Handle(new PreviewSalesRepricingQuery(new List<Guid>(), OldCustomerId, NewCustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeEmpty();
        f.Pricing.Verify(
            p => p.ResolveManyAsync(It.IsAny<PricingBatchContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
