using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Items;

public sealed class ItemTaxConfigTests
{
    [Fact]
    public void Create_con_todos_los_codigos_nulos_no_lanza()
    {
        var act = () => ItemTaxConfig.Create(null, null, null);
        act.Should()
            .NotThrow(
                "un código nulo significa que el impuesto no aplica — ya no es un error de validación."
            );
    }

    [Fact]
    public void Create_con_codigos_presentes_los_persiste_recortados()
    {
        var config = ItemTaxConfig.Create(
            saleVatCode: " 4 ",
            purchaseVatCode: " 4 ",
            exciseTaxCode: " 3072 "
        );

        config.SaleVatCode.Should().Be("4");
        config.PurchaseVatCode.Should().Be("4");
        config.ExciseTaxCode.Should().Be("3072");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_con_codigos_vacios_o_blancos_normaliza_a_null(string? blank)
    {
        var config = ItemTaxConfig.Create(blank, blank, blank);

        config
            .SaleVatCode.Should()
            .BeNull(
                "\"\"/\"   \" deben tratarse igual que NULL — nunca persistir string vacío como código real."
            );
        config.PurchaseVatCode.Should().BeNull();
        config.ExciseTaxCode.Should().BeNull();
    }

    [Fact]
    public void ItemTaxConfig_no_expone_flags_booleanos()
    {
        // Regresión: la infraestructura tributaria CLOSED declara el código como
        // única fuente de verdad — no deben reaparecer AppliesVatOnSale/AppliesVatOnPurchase/AppliesExciseTax.
        typeof(ItemTaxConfig).GetProperty("AppliesVatOnSale").Should().BeNull();
        typeof(ItemTaxConfig).GetProperty("AppliesVatOnPurchase").Should().BeNull();
        typeof(ItemTaxConfig).GetProperty("AppliesExciseTax").Should().BeNull();
    }
}
