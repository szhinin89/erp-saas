using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// SALES-PRESENTATIONS-02: PackagingLevelId/BaseUomCode/ConversionFactor/QuantityInBaseUom en
/// SalesInvoiceDetail. Sin presentación (PackagingLevelId null) el comportamiento debe ser
/// idéntico al existente antes de esta fase — factor 1, QuantityInBaseUom == Quantity.
/// </summary>
public sealed class SalesInvoiceDetailPresentationTests
{
    private static SalesInvoiceDetail CreateLine(
        decimal quantity = 1m,
        decimal conversionFactor = 1m,
        Guid? packagingLevelId = null,
        string uomCode = "UNIT",
        string? baseUomCode = null
    ) =>
        SalesInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            description: "Línea test",
            quantity: quantity,
            unitPrice: 10m,
            vatCode: "4",
            uomCode: uomCode,
            conversionFactor: conversionFactor,
            baseUomCode: baseUomCode,
            packagingLevelId: packagingLevelId
        );

    [Fact]
    public void Create_sin_presentacion_preserva_comportamiento_actual()
    {
        var line = CreateLine(quantity: 5m);

        line.PackagingLevelId.Should().BeNull();
        line.ConversionFactor.Should().Be(1m);
        line.QuantityInBaseUom.Should().Be(5m);
        line.UomCode.Should().Be("UNIT");
        line.BaseUomCode.Should().Be("UNIT");
    }

    [Fact]
    public void Create_con_presentacion_calcula_QuantityInBaseUom_con_el_factor()
    {
        var packagingLevelId = Guid.NewGuid();
        var line = CreateLine(
            quantity: 2m,
            conversionFactor: 12m,
            packagingLevelId: packagingLevelId,
            uomCode: "CAJA",
            baseUomCode: "UNIT"
        );

        line.PackagingLevelId.Should().Be(packagingLevelId);
        line.ConversionFactor.Should().Be(12m);
        line.QuantityInBaseUom.Should().Be(24m);
        line.UomCode.Should().Be("CAJA");
        line.BaseUomCode.Should().Be("UNIT");
    }

    [Fact]
    public void Create_sin_baseUomCode_explicito_usa_uomCode_como_base()
    {
        var line = CreateLine(uomCode: "unit");

        line.BaseUomCode.Should().Be("UNIT");
    }

    [Fact]
    public void Create_rechaza_factor_de_conversion_cero_o_negativo()
    {
        var act = () => CreateLine(conversionFactor: 0m);

        act.Should().Throw<ArgumentException>();
    }
}
