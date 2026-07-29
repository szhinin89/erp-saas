using ERP.Domain.Modules.Items.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Items;

public sealed class ItemVariantBarcodeTests
{
    [Fact]
    public void Create_con_codigo_vacio_lanza_ArgumentException()
    {
        var act = () =>
            ItemVariantBarcode.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "  ",
                "EAN13",
                Guid.NewGuid()
            );
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_marca_isPrimary_segun_parametro()
    {
        var barcode = ItemVariantBarcode.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "7501234567890",
            "EAN13",
            Guid.NewGuid(),
            variantId: Guid.NewGuid(),
            isPrimary: true
        );

        barcode.IsPrimary.Should().BeTrue();
        barcode.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_por_defecto_no_es_principal()
    {
        var barcode = ItemVariantBarcode.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "7501234567890",
            "EAN13",
            Guid.NewGuid()
        );
        barcode.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void MarkAsPrimary_y_UnmarkAsPrimary_alternan_el_estado()
    {
        var barcode = ItemVariantBarcode.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "7501234567890",
            "EAN13",
            Guid.NewGuid()
        );

        barcode.MarkAsPrimary();
        barcode.IsPrimary.Should().BeTrue();

        barcode.UnmarkAsPrimary();
        barcode.IsPrimary.Should().BeFalse();
    }
}
