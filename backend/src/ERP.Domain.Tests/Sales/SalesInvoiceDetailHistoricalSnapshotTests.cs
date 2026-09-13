using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// SALES-HISTORICAL-PRICING-SNAPSHOT-01 — <see cref="SalesInvoiceDetail.SetHistoricalSnapshot"/>
/// congela el snapshot comercial histórico de la línea (bodega/costo/precio de lista/origen del
/// precio/origen del descuento) en el momento de capturar el Draft. Ningún campo de stock (stock
/// disponible/actual) forma parte de este snapshot — ver auditoría previa, deliberadamente fuera
/// de alcance.
/// </summary>
public sealed class SalesInvoiceDetailHistoricalSnapshotTests
{
    private static SalesInvoiceDetail CreateLine() =>
        SalesInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            description: "Línea test",
            quantity: 2m,
            unitPrice: 10m,
            vatCode: "4",
            uomCode: "UNIT"
        );

    [Fact]
    public void Create_deja_todos_los_campos_historicos_en_null()
    {
        var line = CreateLine();

        line.WarehouseName.Should().BeNull();
        line.UnitCostAtSale.Should().BeNull();
        line.TotalCostAtSale.Should().BeNull();
        line.ListPriceAtSale.Should().BeNull();
        line.PriceListId.Should().BeNull();
        line.PriceListName.Should().BeNull();
        line.PricingSource.Should().BeNull();
        line.DiscountSource.Should().BeNull();
        line.DiscountDescription.Should().BeNull();
    }

    [Fact]
    public void SetHistoricalSnapshot_persiste_todos_los_valores_provistos()
    {
        var line = CreateLine();
        var priceListId = Guid.NewGuid();

        line.SetHistoricalSnapshot(
            warehouseName: " Bodega Principal ",
            unitCostAtSale: 6.123456m,
            totalCostAtSale: 12.246912m,
            listPriceAtSale: 11.5m,
            priceListId: priceListId,
            priceListName: " Mayorista ",
            pricingSource: " PercentDiscount:5 (lista) ",
            discountSource: " Manual ",
            discountDescription: " Descuento manual de 5% aplicado en la línea. "
        );

        line.WarehouseName.Should().Be("Bodega Principal");
        line.UnitCostAtSale.Should().Be(6.123456m);
        line.TotalCostAtSale.Should().Be(12.246912m);
        line.ListPriceAtSale.Should().Be(11.5m);
        line.PriceListId.Should().Be(priceListId);
        line.PriceListName.Should().Be("Mayorista");
        line.PricingSource.Should().Be("PercentDiscount:5 (lista)");
        line.DiscountSource.Should().Be("Manual");
        line.DiscountDescription.Should().Be("Descuento manual de 5% aplicado en la línea.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetHistoricalSnapshot_normaliza_strings_vacios_a_null(string? blank)
    {
        var line = CreateLine();

        line.SetHistoricalSnapshot(
            warehouseName: blank,
            unitCostAtSale: null,
            totalCostAtSale: null,
            listPriceAtSale: null,
            priceListId: null,
            priceListName: blank,
            pricingSource: blank,
            discountSource: blank,
            discountDescription: blank
        );

        line.WarehouseName.Should().BeNull();
        line.PriceListName.Should().BeNull();
        line.PricingSource.Should().BeNull();
        line.DiscountSource.Should().BeNull();
        line.DiscountDescription.Should().BeNull();
    }

    [Fact]
    public void SetHistoricalSnapshot_lanza_si_la_linea_ya_esta_congelada()
    {
        var line = CreateLine();
        line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        // Freeze es internal — se invoca igual que lo hace SalesInvoice.Authorize() sobre cada
        // línea, vía reflexión, para no exponer el método a este ensamblado de test.
        typeof(SalesInvoiceDetail)
            .GetMethod("Freeze", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(line, null);

        var act = () =>
            line.SetHistoricalSnapshot(
                warehouseName: "Bodega X",
                unitCostAtSale: null,
                totalCostAtSale: null,
                listPriceAtSale: null,
                priceListId: null,
                priceListName: null,
                pricingSource: null,
                discountSource: null,
                discountDescription: null
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*autorizada*");
    }
}
