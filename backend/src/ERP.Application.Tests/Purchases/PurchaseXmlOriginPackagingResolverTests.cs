using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// FASE 5 PURCHASE-UX — presentación de origen XML de una línea de compra: separada de la operativa,
/// factor resuelto en el servidor desde el nivel del ítem (nunca del body) y snapshot conservado
/// mientras el nivel no cambie.
/// </summary>
public sealed class PurchaseXmlOriginPackagingResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const int FactorDecimals = 6;

    private static Item CreateItem()
    {
        var item = Item.Create(
            TenantId,
            "SKU-FANTA",
            "Fanta 355",
            "Fanta lata 355",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            UserId,
            companyId: Guid.NewGuid()
        );
        item.ReplacePackagingLevels(
            [
                ("Unidad", 1, 1m, "UNIT", null, null, true, false, true),
                ("Sixpack", 2, 6m, "SIX", null, null, false, false, false),
                ("Paca 12", 3, 12m, "PACA", null, null, false, true, false),
            ],
            UserId
        );
        return item;
    }

    private static Guid LevelId(Item item, decimal factor) =>
        item.PackagingLevels.Single(p => p.BaseQuantity == factor).Id;

    private static PurchaseLineInput XmlLine(Guid? originLevelId, Guid? operationalLevelId = null) =>
        new(
            ItemId: Guid.NewGuid(),
            Description: "FANTA",
            Quantity: 23m,
            UnitPrice: 2m,
            VatCode: "10",
            PurchaseReceptionLineId: Guid.NewGuid(),
            PackagingLevelId: operationalLevelId,
            XmlOriginPackagingLevelId: originLevelId
        );

    [Fact]
    public void Origen_x6_elegido_en_la_compra_resuelve_su_factor_aunque_la_operativa_sea_unidad_base()
    {
        var item = CreateItem();
        var line = XmlLine(LevelId(item, 6m), operationalLevelId: LevelId(item, 1m));

        var result = PurchaseXmlOriginPackagingResolver.Resolve(item, line, FactorDecimals, previous: null);

        result.Error.Should().BeNull();
        result.LevelId.Should().Be(LevelId(item, 6m));
        result.Factor.Should().Be(6m);
    }

    [Fact]
    public void Mismo_nivel_ya_persistido_conserva_el_factor_snapshot()
    {
        var item = CreateItem();
        var x12 = LevelId(item, 12m);

        var result = PurchaseXmlOriginPackagingResolver.Resolve(
            item,
            XmlLine(x12),
            FactorDecimals,
            previous: (x12, 10m)
        );

        result.Factor.Should().Be(10m, "el factor es snapshot de la interpretación ya guardada");
    }

    [Fact]
    public void Cambiar_el_nivel_de_origen_resuelve_el_factor_nuevo_desde_el_item()
    {
        var item = CreateItem();

        var result = PurchaseXmlOriginPackagingResolver.Resolve(
            item,
            XmlLine(LevelId(item, 6m)),
            FactorDecimals,
            previous: (LevelId(item, 12m), 12m)
        );

        result.LevelId.Should().Be(LevelId(item, 6m));
        result.Factor.Should().Be(6m);
    }

    [Fact]
    public void Nivel_que_no_pertenece_al_item_se_rechaza()
    {
        var result = PurchaseXmlOriginPackagingResolver.Resolve(
            CreateItem(),
            XmlLine(Guid.NewGuid()),
            FactorDecimals,
            previous: null
        );

        result.Error.Should().Contain("no pertenece al ítem");
    }

    [Fact]
    public void Sin_origen_o_linea_manual_no_hay_origen_borrador_anterior_compatible()
    {
        var item = CreateItem();

        PurchaseXmlOriginPackagingResolver
            .Resolve(item, XmlLine(null), FactorDecimals, previous: null)
            .Should()
            .Be(((Guid?)null, (decimal?)null, (string?)null));

        var manual = XmlLine(LevelId(item, 12m)) with { PurchaseReceptionLineId = null };
        PurchaseXmlOriginPackagingResolver
            .Resolve(item, manual, FactorDecimals, previous: null)
            .LevelId.Should()
            .BeNull();
    }

    [Fact]
    public void Detalle_SetXmlOrigin_exige_linea_de_recepcion_y_nivel_con_factor()
    {
        var xmlDetail = PurchaseInvoiceDetail.Create(
            Guid.NewGuid(),
            TenantId,
            "FANTA",
            23m,
            2m,
            "10",
            "UNIT",
            purchaseReceptionLineId: Guid.NewGuid()
        );
        var levelId = Guid.NewGuid();

        xmlDetail.SetXmlOrigin(levelId, 6m);
        xmlDetail.XmlOriginPackagingLevelId.Should().Be(levelId);
        xmlDetail.XmlOriginFactor.Should().Be(6m);
        // La presentación operativa es independiente del origen.
        xmlDetail.PackagingLevelId.Should().BeNull();

        FluentActions.Invoking(() => xmlDetail.SetXmlOrigin(levelId, null)).Should().Throw<ArgumentException>();

        var manualDetail = PurchaseInvoiceDetail.Create(Guid.NewGuid(), TenantId, "X", 1m, 1m, "10", "UNIT");
        FluentActions.Invoking(() => manualDetail.SetXmlOrigin(levelId, 6m)).Should().Throw<ArgumentException>();
    }
}
