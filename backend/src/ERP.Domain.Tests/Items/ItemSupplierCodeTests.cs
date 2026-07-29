using ERP.Domain.Modules.Items.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Items;

public sealed class ItemSupplierCodeTests
{
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
