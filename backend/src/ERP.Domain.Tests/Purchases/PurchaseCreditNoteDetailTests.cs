using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

public sealed class PurchaseCreditNoteDetailTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PurchaseCreditNoteId = Guid.NewGuid();

    private static PurchaseCreditNoteDetail Create(
        decimal subtotal = 100m,
        decimal vatAmount = 15m,
        string? vatCode = "2",
        decimal? vatRate = 15m
    ) =>
        PurchaseCreditNoteDetail.Create(
            PurchaseCreditNoteId,
            TenantId,
            "Descuento por promoción",
            subtotal,
            vatCode,
            vatRate,
            vatAmount
        );

    [Fact]
    public void Create_con_datos_validos_calcula_TotalAmount()
    {
        var line = Create(subtotal: 100m, vatAmount: 15m);

        line.Subtotal.Should().Be(100m);
        line.VatAmount.Should().Be(15m);
        line.TotalAmount.Should().Be(115m);
        line.VatCode.Should().Be("2");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rechaza_subtotal_no_positivo(decimal subtotal)
    {
        var act = () => Create(subtotal: subtotal);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_TotalAmount_no_positivo_vía_subtotal_y_vat_no_positivos()
    {
        // TotalAmount = Subtotal + VatAmount; con Subtotal <= 0 el dominio ya rechaza antes de
        // llegar a construir un TotalAmount <= 0 (no hay forma legítima de que exista una línea
        // con TotalAmount <= 0 — se documenta explícitamente en vez de duplicar el escenario).
        var act = () => Create(subtotal: 0m, vatAmount: 0m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_IVA_negativo()
    {
        var act = () => Create(vatAmount: -1m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_descripcion_vacia()
    {
        var act = () =>
            PurchaseCreditNoteDetail.Create(
                PurchaseCreditNoteId,
                TenantId,
                " ",
                100m,
                "2",
                15m,
                15m
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_nota_de_credito_destino_vacia()
    {
        var act = () =>
            PurchaseCreditNoteDetail.Create(
                Guid.Empty,
                TenantId,
                "Descuento",
                100m,
                "2",
                15m,
                15m
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_normaliza_VatCode_vacio_a_null()
    {
        var line = Create(vatCode: "   ");

        line.VatCode.Should().BeNull();
    }
}
