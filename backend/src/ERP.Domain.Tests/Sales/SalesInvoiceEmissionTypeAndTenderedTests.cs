using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// POS-EMISSION-TYPE-SNAPSHOT-01 — el tipo de emisión es obligatorio y explícito (sin default
/// Electronic). POS-CASH-TENDERED-01 — el efectivo entregado es dato operacional del pago.
/// </summary>
public sealed class SalesInvoiceEmissionTypeAndTenderedTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static SalesInvoice Draft(EmissionType emissionType) =>
        SalesInvoice.CreateDraft(
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CustomerSnapshot.Create("Cliente", "1710034065", "05"),
            "DRAFT-1",
            new DateOnly(2026, 10, 3),
            Guid.NewGuid(),
            PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0),
            Guid.NewGuid(),
            emissionType
        );

    [Theory]
    [InlineData(EmissionType.Electronic)]
    [InlineData(EmissionType.Physical)]
    public void El_tipo_de_emision_explicito_queda_como_snapshot(EmissionType type)
    {
        Draft(type).EmissionType.Should().Be(type);
    }

    [Fact]
    public void Un_tipo_de_emision_no_definido_no_crea_la_venta_nunca_se_asume_Electronic()
    {
        var act = () => Draft((EmissionType)0);

        act.Should().Throw<ArgumentException>().WithMessage("*tipo de emisión*");
    }

    private static SalesInvoicePayment CashPayment(decimal applied) =>
        SalesInvoicePayment.Create(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            "EFECTIVO",
            "Efectivo",
            applied
        );

    [Fact]
    public void Tendered_mayor_al_aplicado_deriva_el_vuelto_sin_cambiar_el_aplicado()
    {
        var p = CashPayment(14.66m);

        p.SetTenderedAmount(20m);

        p.Amount.Should().Be(14.66m);
        p.TenderedAmount.Should().Be(20m);
        p.ChangeAmount.Should().Be(5.34m);
    }

    [Fact]
    public void Pago_exacto_vuelto_cero()
    {
        var p = CashPayment(14.66m);

        p.SetTenderedAmount(14.66m);

        p.ChangeAmount.Should().Be(0m);
    }

    [Fact]
    public void Sin_tendered_no_hay_vuelto()
    {
        CashPayment(14.66m).ChangeAmount.Should().BeNull();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Tendered_menor_al_aplicado_cero_o_negativo_se_rechaza(decimal tendered)
    {
        var act = () => CashPayment(14.66m).SetTenderedAmount(tendered);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tendered_con_mas_de_dos_decimales_se_rechaza()
    {
        var act = () => CashPayment(14.66m).SetTenderedAmount(20.005m);

        act.Should().Throw<ArgumentException>().WithMessage("*2 decimales*");
    }
}
