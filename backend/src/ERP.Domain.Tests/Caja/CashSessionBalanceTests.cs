using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Caja;

/// <summary>
/// Fase 6 (cierre técnico Caja) — corrige el bug detectado en la validación end-to-end
/// Caja→Ventas: <c>CashSession.CurrentBalance</c> duplicaba <c>OpeningAmount</c> porque
/// <c>CashMovementType.Opening</c> se clasificaba también como "ingreso" en
/// <c>TotalIncome</c>. Escenario exacto reportado: apertura 100 + venta 115 → saldo 215
/// (antes del fix daba 315).
/// </summary>
public sealed class CashSessionBalanceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CashSession OpenWith(decimal openingAmount) =>
        CashSession.Open(
            TenantId, CompanyId, BranchId, UserId,
            Guid.NewGuid(), "CAJA-01", "Caja Principal",
            Guid.NewGuid(), "001",
            openingAmount, UserId);

    [Fact]
    public void Apertura_100_venta_115_saldo_esperado_215()
    {
        var session = OpenWith(100m);

        session.RecordMovement(CashMovementType.SaleIncome, 115m, "Venta 001-001-000000001", UserId,
            CashReferenceType.SalesInvoice, Guid.NewGuid(), "001-001-000000001");

        session.CurrentBalance.Should().Be(215m);
    }

    [Fact]
    public void OpeningAmount_no_se_cuenta_como_ingreso_en_TotalIncome()
    {
        var session = OpenWith(100m);

        session.TotalIncome.Should().Be(0m, "el movimiento Opening no debe clasificarse como ingreso — OpeningAmount ya se suma aparte");
    }

    [Fact]
    public void Sin_movimientos_adicionales_el_saldo_es_igual_a_la_apertura()
    {
        var session = OpenWith(100m);

        session.CurrentBalance.Should().Be(100m);
    }

    [Fact]
    public void Varias_ventas_se_suman_correctamente_sobre_la_apertura()
    {
        var session = OpenWith(100m);

        session.RecordMovement(CashMovementType.SaleIncome, 115m, "Venta 1", UserId);
        session.RecordMovement(CashMovementType.SaleIncome, 50m, "Venta 2", UserId);

        session.CurrentBalance.Should().Be(265m);
    }

    [Fact]
    public void Egresos_se_restan_del_saldo()
    {
        var session = OpenWith(100m);

        session.RecordMovement(CashMovementType.SaleIncome, 115m, "Venta 1", UserId);
        session.RecordMovement(CashMovementType.ManualExpense, 20m, "Compra de insumos", UserId);

        session.CurrentBalance.Should().Be(195m);
    }

    [Fact]
    public void Close_calcula_ExpectedAmount_consistente_con_CurrentBalance_apertura_100_venta_115()
    {
        var session = OpenWith(100m);
        session.RecordMovement(CashMovementType.SaleIncome, 115m, "Venta 001-001-000000001", UserId);

        session.Close(UserId, new List<CashClosingCount>
        {
            CashClosingCount.Create(session.Id, TenantId, 20m, "Billetes de $20", 10),
            CashClosingCount.Create(session.Id, TenantId, 1m, "Billetes de $1", 15),
        });

        session.ExpectedAmount.Should().Be(215m);
        session.CountedAmount.Should().Be(215m);
        session.Difference.Should().Be(0m);
    }
}
