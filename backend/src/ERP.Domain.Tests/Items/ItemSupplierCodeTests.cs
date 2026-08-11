using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Items;

public sealed class ItemSupplierCodeTests
{
    private static Item CreateItem()
    {
        var item = Item.Create(
            Guid.NewGuid(),
            "SKU-001",
            "Bebida",
            "Bebida retornable",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            Guid.NewGuid()
        );

        item.ReplacePackagingLevels(
            [
                ("Unidad", 1, 1m, "UNIT", null, null, true, false, true),
                ("Paca 12", 2, 12m, "PACA", null, null, false, true, false),
            ],
            Guid.NewGuid()
        );

        return item;
    }

    [Fact]
    public void Create_con_codigo_vacio_lanza_ArgumentException()
    {
        var act = () =>
            ItemSupplierCode.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                " ",
                Guid.NewGuid()
            );
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_sin_proveedor_lanza_ArgumentException()
    {
        var act = () =>
            ItemSupplierCode.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.Empty,
                "PROV-001",
                Guid.NewGuid()
            );
        act.Should()
            .Throw<ArgumentException>(
                "el proveedor es obligatorio (Fase 2) — no existe código de proveedor sin proveedor."
            );
    }

    [Fact]
    public void Create_con_proveedor_es_valido()
    {
        var supplierId = Guid.NewGuid();
        var code = ItemSupplierCode.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            supplierId,
            "PROV-001",
            Guid.NewGuid()
        );

        code.SupplierId.Should().Be(supplierId);
        code.IsPrimary.Should().BeFalse();
        code.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_con_proveedor_y_principal_los_persiste()
    {
        var supplierId = Guid.NewGuid();
        var code = ItemSupplierCode.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            supplierId,
            "PROV-001",
            Guid.NewGuid(),
            isPrimary: true
        );

        code.SupplierId.Should().Be(supplierId);
        code.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Create_con_presentacion_de_compra_persiste_packagingLevelId()
    {
        var packagingLevelId = Guid.NewGuid();

        var code = ItemSupplierCode.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "PROV-PACA-12",
            Guid.NewGuid(),
            packagingLevelId: packagingLevelId
        );

        code.PackagingLevelId.Should().Be(packagingLevelId);
    }

    [Fact]
    public void Create_con_presentacion_vacia_lanza_ArgumentException()
    {
        var act = () =>
            ItemSupplierCode.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "PROV-PACA-12",
                Guid.NewGuid(),
                packagingLevelId: Guid.Empty
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddSupplierCode_rechaza_presentacion_que_no_pertenece_al_item()
    {
        var item = CreateItem();
        var otherItem = CreateItem();
        var otherPackagingId = otherItem.PackagingLevels.Single(p => p.UomCode == "PACA").Id;

        var act = () =>
            item.AddSupplierCode(
                "PROV-OTRA-PRESENTACION",
                false,
                Guid.NewGuid(),
                Guid.NewGuid(),
                otherPackagingId
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*nivel de empaque*no pertenece al ítem*");
    }

    [Fact]
    public void AddSupplierCode_acepta_presentacion_activa_del_item()
    {
        var item = CreateItem();
        var purchasePackagingId = item.PackagingLevels.Single(p => p.UomCode == "PACA").Id;

        var code = item.AddSupplierCode(
            "PROV-PACA-12",
            false,
            Guid.NewGuid(),
            Guid.NewGuid(),
            purchasePackagingId
        );

        code.PackagingLevelId.Should().Be(purchasePackagingId);
    }

    [Fact]
    public void SetSupplierCodePackagingLevel_actualiza_presentacion_del_mismo_item()
    {
        var item = CreateItem();
        var supplierId = Guid.NewGuid();
        var updatedBy = Guid.NewGuid();
        var packagingId = item.PackagingLevels.Single(p => p.UomCode == "PACA").Id;
        item.AddSupplierCode("PROV-001", false, supplierId, updatedBy);

        item.SetSupplierCodePackagingLevel(supplierId, "PROV-001", packagingId, updatedBy);

        item.SupplierCodes.Single().PackagingLevelId.Should().Be(packagingId);
    }

    [Fact]
    public void SetSupplierCodePackagingLevel_rechaza_presentacion_de_otro_item()
    {
        var item = CreateItem();
        var otherItem = CreateItem();
        var supplierId = Guid.NewGuid();
        var updatedBy = Guid.NewGuid();
        var otherPackagingId = otherItem.PackagingLevels.Single(p => p.UomCode == "PACA").Id;
        item.AddSupplierCode("PROV-001", false, supplierId, updatedBy);

        var act = () =>
            item.SetSupplierCodePackagingLevel(supplierId, "PROV-001", otherPackagingId, updatedBy);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*nivel de empaque*no pertenece al ítem*");
    }

    [Fact]
    public void MarkAsPrimary_y_UnmarkAsPrimary_alternan_el_estado()
    {
        var code = ItemSupplierCode.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "PROV-001",
            Guid.NewGuid()
        );

        code.MarkAsPrimary();
        code.IsPrimary.Should().BeTrue();

        code.UnmarkAsPrimary();
        code.IsPrimary.Should().BeFalse();
    }
}
