using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.UseCases.GetSalesItemPricing;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// SALES-ITEM-PRICING-EF-MULTIPLE-COLLECTION-WARNING-01: GetSalesItemPricingQueryHandler pasó de
/// IItemRepository.GetByIdAsync (7 Includes de colección — disparaba el warning de EF Core
/// "multiple collection navigation" en GET /api/v1/sales/items/{itemId}/pricing) a
/// GetByIdWithSpecialTaxConfigurationsAsync (un solo Include: SpecialTaxConfigurations, la única
/// colección que este handler realmente lee, para resolver ICE). Estos tests fijan el contrato:
/// el handler llama al método liviano nuevo, nunca al pesado, y el pricing devuelto (precio, IVA,
/// ICE, descuento máximo) es exactamente el mismo que antes del cambio — el fix es solo de
/// performance en la query, no toca ningún cálculo.
/// </summary>
public sealed class GetSalesItemPricingQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IPricingResolver> PricingResolver { get; } = new();
        public Mock<ISriTaxResolver> TaxResolver { get; } = new();
        public Mock<ICompanySpecialTaxResponsibilityRepository> CompanyTaxRepo { get; } = new();
        public Mock<ICurrentTenant> CurrentTenant { get; } = new();
        public Mock<ICurrentCompany> CurrentCompany { get; } = new();

        public Fixture()
        {
            CurrentTenant.Setup(t => t.TenantId).Returns(TenantId);
            CurrentCompany.Setup(c => c.CompanyId).Returns(CompanyId);
            CompanyTaxRepo
                .Setup(r =>
                    r.GetResponsibleSriTaxCategoryCodesAsync(
                        CompanyId,
                        TenantId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<string>());
        }

        public GetSalesItemPricingQueryHandler BuildHandler() =>
            new(
                ItemRepo.Object,
                PricingResolver.Object,
                TaxResolver.Object,
                CompanyTaxRepo.Object,
                CurrentTenant.Object,
                CurrentCompany.Object
            );
    }

    private static Item CreateItem(bool isForSale = true, bool isActive = true, string? vatCode = "10")
    {
        var item = Item.Create(
            TenantId,
            "SKU-001",
            "Coca Cola 500ML",
            "Coca Cola botella 500ML",
            ItemTypeId,
            "UNIT",
            ItemTaxConfig.Create(vatCode, "10"),
            ItemSaleConfig.Create(isForSale: isForSale, maxDiscountPercent: 8m),
            ItemStockConfig.Create(),
            UserId
        );
        if (!isActive) item.Disable(UserId);
        return item;
    }

    private static PricingResult SamplePricing(Guid itemId) =>
        new(
            itemId,
            Guid.NewGuid(),
            "GEN",
            "Lista General",
            "USD",
            24.30m,
            "Descuento 5% (regla general)",
            23.09m,
            "Descuento 5% (regla general)"
        );

    [Fact]
    public async Task Llama_GetByIdWithSpecialTaxConfigurationsAsync_nunca_GetByIdAsync()
    {
        var item = CreateItem();
        var f = new Fixture();
        f.ItemRepo
            .Setup(r =>
                r.GetByIdWithSpecialTaxConfigurationsAsync(item.Id, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(item);
        f.PricingResolver
            .Setup(p => p.ResolveAsync(new PricingContext(item.Id, null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.Success(SamplePricing(item.Id)));

        var result = await f.BuildHandler()
            .Handle(new GetSalesItemPricingQuery(item.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.ItemRepo.Verify(
            r => r.GetByIdWithSpecialTaxConfigurationsAsync(
                item.Id,
                TenantId,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
        f.ItemRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Devuelve_el_pricing_resuelto_por_IPricingResolver_sin_alterarlo()
    {
        var item = CreateItem();
        var f = new Fixture();
        var pricing = SamplePricing(item.Id);
        f.ItemRepo
            .Setup(r =>
                r.GetByIdWithSpecialTaxConfigurationsAsync(item.Id, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(item);
        f.PricingResolver
            .Setup(p => p.ResolveAsync(new PricingContext(item.Id, null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.Success(pricing));
        f.TaxResolver
            .Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));

        var result = await f.BuildHandler()
            .Handle(new GetSalesItemPricingQuery(item.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var dto = result.Value!;
        dto.ItemId.Should().Be(item.Id);
        dto.UnitPrice.Should().Be(pricing.UnitPrice);
        dto.BasePrice.Should().Be(pricing.BasePrice);
        dto.PriceListCode.Should().Be(pricing.PriceListCode);
        dto.PriceListName.Should().Be(pricing.PriceListName);
        dto.DiscountDescription.Should().Be(pricing.RuleDescription);
        // SALES-PRICING-UX-TRACEABILITY-07C: el id de lista pasa tal cual (null = PVP) para que la
        // UI distinga PVP real del sentinel de nombre, sin hardcodear "Precio base".
        dto.PriceListId.Should().Be(pricing.PriceListId);
        dto.VatCode.Should().Be("10");
        dto.VatName.Should().Be("IVA 15%");
        dto.MaxDiscountPercent.Should().Be(8m);
        dto.IceCode.Should().BeNull();
        dto.IceName.Should().BeNull();
    }

    [Fact]
    public async Task Resuelve_ICE_desde_SpecialTaxConfigurations_cuando_la_empresa_es_responsable()
    {
        var item = CreateItem();
        item.ReplaceSpecialTaxConfigurations(
            [(SriTaxCategoryCodes.Ice, "3021")],
            UserId
        );
        var f = new Fixture();
        f.CompanyTaxRepo
            .Setup(r =>
                r.GetResponsibleSriTaxCategoryCodesAsync(
                    CompanyId,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { SriTaxCategoryCodes.Ice });
        f.ItemRepo
            .Setup(r =>
                r.GetByIdWithSpecialTaxConfigurationsAsync(item.Id, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(item);
        f.PricingResolver
            .Setup(p => p.ResolveAsync(new PricingContext(item.Id, null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.Success(SamplePricing(item.Id)));
        f.TaxResolver
            .Setup(t => t.GetIceRateWithNameAsync("3021", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(0.5m, "ICE 50%"));

        var result = await f.BuildHandler()
            .Handle(new GetSalesItemPricingQuery(item.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.IceCode.Should().Be("3021");
        result.Value!.IceName.Should().Be("ICE 50%");
    }

    [Fact]
    public async Task Propaga_el_CustomerId_de_la_query_al_PricingContext()
    {
        // SALES-CONTEXTUAL-PRICING-READ-06A: Sales solo informa el cliente actual — nunca decide
        // qué lista corresponde. El handler debe pasar exactamente el CustomerId recibido.
        var item = CreateItem();
        var customerId = Guid.NewGuid();
        var f = new Fixture();
        f.ItemRepo
            .Setup(r =>
                r.GetByIdWithSpecialTaxConfigurationsAsync(item.Id, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(item);
        f.PricingResolver
            .Setup(p => p.ResolveAsync(new PricingContext(item.Id, customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.Success(SamplePricing(item.Id)));

        var result = await f.BuildHandler()
            .Handle(new GetSalesItemPricingQuery(item.Id, customerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.PricingResolver.Verify(
            p => p.ResolveAsync(new PricingContext(item.Id, customerId), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Item_inexistente_devuelve_NotFound()
    {
        var f = new Fixture();
        var missingId = Guid.NewGuid();
        f.ItemRepo
            .Setup(r =>
                r.GetByIdWithSpecialTaxConfigurationsAsync(missingId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Item?)null);

        var result = await f.BuildHandler()
            .Handle(new GetSalesItemPricingQuery(missingId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Item_inactivo_o_no_habilitado_para_venta_devuelve_ValidationFailure()
    {
        var item = CreateItem(isForSale: false);
        var f = new Fixture();
        f.ItemRepo
            .Setup(r =>
                r.GetByIdWithSpecialTaxConfigurationsAsync(item.Id, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(item);

        var result = await f.BuildHandler()
            .Handle(new GetSalesItemPricingQuery(item.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.PricingResolver.Verify(
            p => p.ResolveAsync(It.IsAny<PricingContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
