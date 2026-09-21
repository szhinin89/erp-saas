using ERP.Application.Tests.TestSupport;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Pricing;

/// <summary>
/// PRICE-LIST-COMPANY-CLOCK-01 — regresión: la vigencia de una PriceList (ValidFrom/ValidUntil)
/// debe evaluarse contra la fecha calendario de la empresa (<see cref="ICompanyClock"/>), nunca
/// contra <c>DateTime.UtcNow</c>. Repro real: Ecuador (UTC-5) 17/09 19:53 → UTC ya es 18/09
/// 00:53 — una lista con ValidUntil = 17/09 debe seguir vigente para esa venta.
/// </summary>
public sealed class PricingResolverCompanyClockTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

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
            // PRICING-LIST-ASSIGNMENT-ENFORCEMENT-02: esta suite cubre vigencia por CompanyClock,
            // no asignación — se asume asignado y activo por defecto para no romper esos casos.
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

    private static Item CreateItem(decimal basePrice = 24.30m) =>
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

    private static PriceList CreatePriceList(DateOnly? validFrom, DateOnly? validUntil) =>
        PriceList.Create(
            TenantId,
            CompanyId,
            "GENERAL",
            "Lista General",
            "USD",
            isDefault: true,
            createdBy: UserId,
            validFrom: validFrom,
            validUntil: validUntil
        );

    [Fact]
    public async Task Ecuador_19h53_con_UTC_ya_en_dia_siguiente_lista_con_ValidUntil_hoy_sigue_vigente()
    {
        // Repro exacto del bug reportado: ValidUntil = 17/09, "hoy operativo" de la empresa (vía
        // ICompanyClock, que ya convierte UTC → America/Guayaquil) también es 17/09 — aunque el
        // reloj UTC del servidor ya marque 18/09.
        var companyToday = new DateOnly(2026, 9, 17);
        var f = new Fixture();
        f.CompanyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);

        var item = CreateItem();
        var priceList = CreatePriceList(new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 17));

        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetAllAsync(TenantId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { priceList });

        var result = await f.Build().ResolveAsync(item.Id, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.CompanyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Dia_local_siguiente_al_ValidUntil_la_lista_se_ignora_y_usa_PVP_base_sin_error()
    {
        // PRICE-LIST-EXPIRED-FALLBACK-PVP-01: una lista vencida ya NUNCA bloquea la venta — se
        // ignora y el precio cae al PVP/BaseSalePrice del ítem tal cual.
        var companyToday = new DateOnly(2026, 9, 18);
        var f = new Fixture();
        f.CompanyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);

        var item = CreateItem();
        var priceList = CreatePriceList(new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 17));

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
        result.Value!.UnitPrice.Should().Be(item.BaseSalePrice!.Value);
        result.Value!.BasePrice.Should().Be(item.BaseSalePrice!.Value);
    }

    [Fact]
    public async Task ValidUntil_es_inclusivo_el_dia_exacto_de_vencimiento_sigue_vigente()
    {
        var companyToday = new DateOnly(2026, 9, 17);
        var f = new Fixture();
        f.CompanyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);

        var item = CreateItem();
        var priceList = CreatePriceList(new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 17));

        f.Items
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);

        var result = await f.Build().ResolveAsync(item.Id, priceList.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }
}
