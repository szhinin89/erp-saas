using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Pricing.UseCases.ItemPricingSimulation;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Pricing;

/// <summary>
/// PRICE-LIST-COMPANY-CLOCK-01 — regresión análoga a
/// <see cref="PricingResolverCompanyClockTests"/>: la simulación de pricing filtra las
/// PriceList por vigencia usando la fecha operativa de la empresa (<see cref="ICompanyClock"/>),
/// no <c>DateTime.UtcNow</c>.
/// </summary>
public sealed class GetItemPricingSimulationCompanyClockTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

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
        }

        public GetItemPricingSimulationQueryHandler Build() =>
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

    private static PriceList CreatePriceList(DateOnly validUntil) =>
        PriceList.Create(
            TenantId,
            CompanyId,
            "GENERAL",
            "Lista General",
            "USD",
            isDefault: true,
            createdBy: UserId,
            validFrom: new DateOnly(2026, 9, 9),
            validUntil: validUntil
        );

    [Fact]
    public async Task Ecuador_19h53_con_UTC_en_dia_siguiente_lista_con_ValidUntil_hoy_sigue_incluida()
    {
        var companyToday = new DateOnly(2026, 9, 17);
        var f = new Fixture();
        f.CompanyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);
        var priceList = CreatePriceList(new DateOnly(2026, 9, 17));
        f.PriceLists
            .Setup(r => r.GetAllAsync(TenantId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { priceList });

        var result = await f.Build()
            .Handle(
                new GetItemPricingSimulationQuery(null, BaseSalePriceOverride: 24.30m),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Should().ContainSingle(r => r.PriceListId == priceList.Id);
        f.CompanyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Dia_local_siguiente_al_ValidUntil_la_lista_ya_no_se_incluye()
    {
        var companyToday = new DateOnly(2026, 9, 18);
        var f = new Fixture();
        f.CompanyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);
        var priceList = CreatePriceList(new DateOnly(2026, 9, 17));
        f.PriceLists
            .Setup(r => r.GetAllAsync(TenantId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { priceList });

        var result = await f.Build()
            .Handle(
                new GetItemPricingSimulationQuery(null, BaseSalePriceOverride: 24.30m),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Should().BeEmpty();
    }
}
