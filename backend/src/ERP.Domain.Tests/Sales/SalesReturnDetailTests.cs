using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

public sealed class SalesReturnDetailTests
{
    private static readonly Guid ReturnId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OriginalInvoiceDetailId = Guid.NewGuid();

    private static SalesReturnDetail Create(decimal quantity = 2m, decimal unitPrice = 10m) =>
        SalesReturnDetail.Create(
            ReturnId,
            TenantId,
            OriginalInvoiceDetailId,
            "Producto de prueba",
            quantity,
            unitPrice,
            discountPct: 0m,
            vatCode: "2",
            vatRate: 15m,
            uomCode: "UNIT"
        );

    [Fact]
    public void Create_con_datos_validos_calcula_montos_con_la_cantidad_devuelta()
    {
        var line = Create(quantity: 2m, unitPrice: 10m);

        line.LineSubtotal.Should().Be(20m);
        line.TaxableBase.Should().Be(20m);
        line.VatAmount.Should().Be(3m); // 15% de 20
        line.TaxInclusiveTotal.Should().Be(23m);
        line.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public void Create_hereda_snapshot_fiscal_sin_recalcular_contra_el_item_actual()
    {
        var line = SalesReturnDetail.Create(
            ReturnId,
            TenantId,
            OriginalInvoiceDetailId,
            "Producto con ICE",
            quantity: 1m,
            unitPrice: 100m,
            discountPct: 0m,
            vatCode: "2",
            vatRate: 15m,
            uomCode: "UNIT",
            iceCode: "3010",
            iceRate: 10m
        );

        line.VatCode.Should().Be("2");
        line.VatRate.Should().Be(15m);
        line.IceCode.Should().Be("3010");
        line.IceRate.Should().Be(10m);
        line.IceAmount.Should().Be(10m); // 10% de 100
        line.VatAmount.Should().Be(16.5m); // 15% de (100 + 10)
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rechaza_cantidad_cero_o_negativa(decimal quantity)
    {
        var act = () =>
            SalesReturnDetail.Create(
                ReturnId,
                TenantId,
                OriginalInvoiceDetailId,
                "Producto de prueba",
                quantity,
                unitPrice: 10m,
                discountPct: 0m,
                vatCode: "2",
                vatRate: 15m,
                uomCode: "UNIT"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_descripcion_vacia()
    {
        var act = () =>
            SalesReturnDetail.Create(
                ReturnId,
                TenantId,
                OriginalInvoiceDetailId,
                " ",
                quantity: 1m,
                unitPrice: 10m,
                discountPct: 0m,
                vatCode: "2",
                vatRate: 15m,
                uomCode: "UNIT"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_linea_original_vacia()
    {
        var act = () =>
            SalesReturnDetail.Create(
                ReturnId,
                TenantId,
                Guid.Empty,
                "Producto de prueba",
                quantity: 1m,
                unitPrice: 10m,
                discountPct: 0m,
                vatCode: "2",
                vatRate: 15m,
                uomCode: "UNIT"
            );

        act.Should().Throw<ArgumentException>();
    }

    // ── SALES-PRESENTATIONS-02 ──────────────────────────────────────────

    [Fact]
    public void Create_sin_presentacion_preserva_comportamiento_actual()
    {
        var line = Create(quantity: 3m);

        line.PackagingLevelId.Should().BeNull();
        line.ConversionFactor.Should().Be(1m);
        line.QuantityInBaseUom.Should().Be(3m);
        line.BaseUomCode.Should().Be("UNIT");
    }

    [Fact]
    public void Create_con_presentacion_calcula_QuantityInBaseUom_con_el_factor_heredado()
    {
        var packagingLevelId = Guid.NewGuid();
        var line = SalesReturnDetail.Create(
            ReturnId,
            TenantId,
            OriginalInvoiceDetailId,
            "Caja x12",
            quantity: 1m,
            unitPrice: 120m,
            discountPct: 0m,
            vatCode: "2",
            vatRate: 15m,
            uomCode: "CAJA",
            packagingLevelId: packagingLevelId,
            conversionFactor: 12m,
            baseUomCode: "UNIT"
        );

        line.PackagingLevelId.Should().Be(packagingLevelId);
        line.ConversionFactor.Should().Be(12m);
        line.QuantityInBaseUom.Should().Be(12m);
        line.BaseUomCode.Should().Be("UNIT");
        line.UomCode.Should().Be("CAJA");
    }
}
