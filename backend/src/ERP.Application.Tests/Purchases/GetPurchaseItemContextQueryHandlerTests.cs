using ERP.Application.Common;
using ERP.Application.Modules.Companies.UseCases.DecimalConfig;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases.GetPurchaseItemContext;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.2/Subfase 5A) — GetPurchaseItemContextQueryHandler
/// resuelve ICE desde ItemSpecialTaxConfiguration (colección 1:N), no desde el legacy compatibility
/// mirror TaxConfig.ExciseTaxCode. Cubre ambos casos: ítem con ICE activo configurado, e ítem sin
/// configuración de ICE (comportamiento por defecto — nunca ICE falso).
/// </summary>
public sealed class GetPurchaseItemContextQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<IPricingResolver> PricingResolver { get; } = new();
        public Mock<ISriTaxResolver> TaxResolver { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<IDecimalConfigRepository> DecimalConfigRepo { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            DecimalConfigRepo
                .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DecimalConfigDto(2, 2, 4, 2, 2));
            StockRepo
                .Setup(r =>
                    r.GetStockAsync(TenantId, WarehouseId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync((ERP.Domain.Modules.Inventory.Entities.CurrentStock?)null);
            StockRepo
                .Setup(r =>
                    r.GetLastPurchaseCostAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        WarehouseId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((decimal?)null);
            PricingResolver
                .Setup(r =>
                    r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Result<PricingResult>.NotFound("sin precio configurado"));
        }

        public GetPurchaseItemContextQueryHandler BuildHandler() =>
            new(
                ItemRepo.Object,
                StockRepo.Object,
                PricingResolver.Object,
                TaxResolver.Object,
                Tenant.Object,
                Company.Object,
                DecimalConfigRepo.Object
            );
    }

    private static Item CreateItem(string? iceCatalogCode)
    {
        var item = Item.Create(
            TenantId,
            "SKU-ICE-001",
            "Bebida gaseosa",
            "Bebida gaseosa con azúcar",
            ItemTypeId,
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            UserId
        );

        if (iceCatalogCode is not null)
            item.ReplaceSpecialTaxConfigurations(
                [("3", iceCatalogCode)],
                UserId
            );

        return item;
    }

    [Fact]
    public async Task Item_con_ItemSpecialTaxConfiguration_de_ICE_activo_resuelve_HasIce_true()
    {
        var f = new Fixture();
        var item = CreateItem(iceCatalogCode: "3041");
        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        f.TaxResolver
            .Setup(r => r.GetIceRateAsync("3041", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);

        var result = await f.BuildHandler()
            .Handle(new GetPurchaseItemContextQuery(item.Id, WarehouseId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasIce.Should().BeTrue();
        result.Value!.ExciseTaxCode.Should().Be("3041");
        result.Value!.IcePercent.Should().Be(10m);
        f.TaxResolver.Verify(
            r => r.GetIceRateAsync("3041", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Item_sin_ItemSpecialTaxConfiguration_de_ICE_resuelve_HasIce_false_sin_ICE_falso()
    {
        var f = new Fixture();
        var item = CreateItem(iceCatalogCode: null);
        f.ItemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await f.BuildHandler()
            .Handle(new GetPurchaseItemContextQuery(item.Id, WarehouseId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasIce.Should().BeFalse();
        result.Value!.ExciseTaxCode.Should().BeNull();
        result.Value!.IcePercent.Should().Be(0m);
        f.TaxResolver.Verify(
            r => r.GetIceRateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
