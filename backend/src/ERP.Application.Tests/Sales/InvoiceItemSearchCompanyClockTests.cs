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
/// PRICE-LIST-COMPANY-CLOCK-01 — regresión análoga a
/// <c>ERP.Application.Tests.Pricing.PricingResolverCompanyClockTests</c>: el buscador de ítems
/// para factura decide si la lista de precios default aplica descuento comparando su vigencia
/// contra la fecha operativa de la empresa (<see cref="ICompanyClock"/>), no contra UTC.
/// </summary>
public sealed class InvoiceItemSearchCompanyClockTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IInvoiceItemSearchRepository> Repo { get; } = new();
        public Mock<ISriCatalogResolver> Sri { get; } = new();
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

            var match = new InvoiceItemMatch(
                ItemId,
                "SKU-001",
                "Item de prueba",
                null,
                "UNI",
                true,
                "Bodega Principal",
                10m,
                5m,
                24.30m,
                null,
                null,
                "UNI",
                Array.Empty<InvoiceItemPackagingLevelDto>(),
                null
            );
            Repo.Setup(r =>
                    r.SearchAsync(
                        TenantId,
                        CompanyId,
                        It.IsAny<string>(),
                        It.IsAny<Guid?>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new[] { match });

            Sri.Setup(s =>
                    s.ResolveVatRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(new Dictionary<string, SriVatInfo>());
            Sri.Setup(s =>
                    s.ResolveIceRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(new Dictionary<string, SriIceInfo>());

            Rules
                .Setup(r =>
                    r.GetByPriceListAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Array.Empty<PricingRule>());
            // Esta suite cubre vigencia por CompanyClock, no asignación (eso lo cubre
            // SalesItemSearchPriceListAssignmentTests) — el ítem de prueba se asume asignado y
            // activo en la lista default por defecto.
            Assignments
                .Setup(a =>
                    a.GetByPriceListAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(new[] { PriceListItem.Create(TenantId, CompanyId, Guid.NewGuid(), ItemId, UserId) });

            var percentDiscount = new Mock<IPricingAdjustmentStrategy>();
            percentDiscount
                .Setup(s => s.Apply(It.IsAny<decimal>(), It.IsAny<decimal>()))
                .Returns((decimal basePrice, decimal pct) => basePrice * (1 - pct / 100m));
            Strategies
                .Setup(s => s.Resolve(PricingRuleType.PercentDiscount))
                .Returns(percentDiscount.Object);
        }

        public SearchItemsForInvoiceHandler Build() =>
            new(
                Repo.Object,
                Sri.Object,
                PriceLists.Object,
                Rules.Object,
                Assignments.Object,
                Strategies.Object,
                Tenant.Object,
                Company.Object,
                CompanyClock.Object
            );
    }

    private static PriceList CreateDefaultListWithGeneralDiscount(DateOnly validUntil) =>
        PriceList.Create(
            TenantId,
            CompanyId,
            "GENERAL",
            "Lista General",
            "USD",
            isDefault: true,
            createdBy: UserId,
            validFrom: new DateOnly(2026, 9, 9),
            validUntil: validUntil,
            ruleType: PricingRuleType.PercentDiscount,
            ruleValue: 5m
        );

    [Fact]
    public async Task Ecuador_19h53_con_UTC_en_dia_siguiente_lista_default_con_ValidUntil_hoy_sigue_aplicando_descuento()
    {
        var companyToday = new DateOnly(2026, 9, 17);
        var f = new Fixture();
        f.CompanyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);
        var defaultList = CreateDefaultListWithGeneralDiscount(new DateOnly(2026, 9, 17));
        f.PriceLists
            .Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultList);

        var result = await f.Build()
            .Handle(new SearchItemsForInvoiceQuery("prue", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Single().DiscountedSalePriceWithoutTax.Should().NotBeNull();
        f.CompanyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Dia_local_siguiente_al_ValidUntil_la_lista_default_ya_no_aplica_descuento()
    {
        var companyToday = new DateOnly(2026, 9, 18);
        var f = new Fixture();
        f.CompanyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);
        var defaultList = CreateDefaultListWithGeneralDiscount(new DateOnly(2026, 9, 17));
        f.PriceLists
            .Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultList);

        var result = await f.Build()
            .Handle(new SearchItemsForInvoiceQuery("prue", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Single().DiscountedSalePriceWithoutTax.Should().BeNull();
    }
}
