using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// ADR-033, Fase 4 — SalesInvoice.GeneratePaymentSchedule/ReplacePaymentSchedule y
/// SalesPaymentSchedule.Create.
/// </summary>
public sealed class SalesPaymentScheduleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CustomerSnapshot Customer() =>
        CustomerSnapshot.Create("Cliente Test", "0999999999", "05");

    private static SalesInvoice CreateDraftWithLine(
        decimal unitPrice = 100m,
        int installments = 1,
        int daysBetween = 0,
        DateOnly? issueDate = null
    )
    {
        var pt = PaymentTermSnapshot.Create(Guid.NewGuid(), "Test", installments, daysBetween);
        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            Customer(),
            "DRAFT-TEST",
            issueDate ?? new DateOnly(2026, 1, 1),
            UserId,
            pt,
            cashSessionId: Guid.NewGuid()
        );
        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: unitPrice,
            vatCode: "0",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);
        return inv;
    }

    // ── SalesPaymentSchedule.Create ─────────────────────────────────────

    [Fact]
    public void Create_rechaza_numero_de_cuota_menor_a_1()
    {
        var act = () => SalesPaymentSchedule.Create(Guid.NewGuid(), TenantId, 0, new DateOnly(2026, 1, 1), 10m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_monto_cero_o_negativo()
    {
        var act = () => SalesPaymentSchedule.Create(Guid.NewGuid(), TenantId, 1, new DateOnly(2026, 1, 1), 0m);
        act.Should().Throw<ArgumentException>();
    }

    // ── GeneratePaymentSchedule ──────────────────────────────────────────

    [Fact]
    public void GeneratePaymentSchedule_contado_genera_una_cuota_con_el_total()
    {
        var inv = CreateDraftWithLine(unitPrice: 100m, installments: 1, daysBetween: 0);

        inv.GeneratePaymentSchedule();

        inv.PaymentSchedules.Should().ContainSingle();
        inv.PaymentSchedules[0].Amount.Should().Be(inv.GrandTotal);
        inv.PaymentSchedules[0].DueDate.Should().Be(inv.IssueDate);
        inv.IsPaymentScheduleManual.Should().BeFalse();
    }

    [Fact]
    public void GeneratePaymentSchedule_varias_cuotas_suma_exacta_al_total()
    {
        var inv = CreateDraftWithLine(unitPrice: 100m, installments: 3, daysBetween: 30);

        inv.GeneratePaymentSchedule();

        inv.PaymentSchedules.Should().HaveCount(3);
        inv.PaymentSchedules.Sum(s => s.Amount).Should().Be(inv.GrandTotal);
        inv.PaymentSchedules[2].DueDate.Should().Be(inv.IssueDate.AddDays(90));
    }

    [Fact]
    public void GeneratePaymentSchedule_regenera_y_reemplaza_cronograma_previo()
    {
        var inv = CreateDraftWithLine(unitPrice: 100m, installments: 1, daysBetween: 0);
        inv.GeneratePaymentSchedule();
        var firstId = inv.PaymentSchedules[0].Id;

        inv.GeneratePaymentSchedule();

        inv.PaymentSchedules.Should().ContainSingle();
        inv.PaymentSchedules[0].Id.Should().NotBe(firstId);
    }

    [Fact]
    public void GeneratePaymentSchedule_fuera_de_Draft_lanza()
    {
        var inv = CreateDraftWithLine();
        var payment = SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", inv.GrandTotal);
        inv.ReplacePayments(new[] { payment }, UserId);
        inv.Authorize(UserId);

        var act = () => inv.GeneratePaymentSchedule();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── ReplacePaymentSchedule ───────────────────────────────────────────

    [Fact]
    public void ReplacePaymentSchedule_valido_marca_IsPaymentScheduleManual()
    {
        var inv = CreateDraftWithLine(unitPrice: 100m);

        inv.ReplacePaymentSchedule(
            new List<(int, DateOnly, decimal, string?)>
            {
                (1, inv.IssueDate.AddDays(10), 50m, "Cuota 1"),
                (2, inv.IssueDate.AddDays(20), 50m, null),
            }
        );

        inv.PaymentSchedules.Should().HaveCount(2);
        inv.IsPaymentScheduleManual.Should().BeTrue();
        inv.PaymentSchedules.Sum(s => s.Amount).Should().Be(100m);
    }

    [Fact]
    public void ReplacePaymentSchedule_rechaza_suma_distinta_al_total()
    {
        var inv = CreateDraftWithLine(unitPrice: 100m);

        var act = () =>
            inv.ReplacePaymentSchedule(
                new List<(int, DateOnly, decimal, string?)> { (1, inv.IssueDate, 90m, null) }
            );

        act.Should().Throw<InvalidOperationException>().WithMessage("*no coincide*");
    }

    [Fact]
    public void ReplacePaymentSchedule_rechaza_fecha_anterior_a_IssueDate()
    {
        var inv = CreateDraftWithLine(unitPrice: 100m);

        var act = () =>
            inv.ReplacePaymentSchedule(
                new List<(int, DateOnly, decimal, string?)>
                {
                    (1, inv.IssueDate.AddDays(-1), 100m, null),
                }
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReplacePaymentSchedule_rechaza_numero_de_cuota_duplicado()
    {
        var inv = CreateDraftWithLine(unitPrice: 100m);

        var act = () =>
            inv.ReplacePaymentSchedule(
                new List<(int, DateOnly, decimal, string?)>
                {
                    (1, inv.IssueDate, 50m, null),
                    (1, inv.IssueDate.AddDays(1), 50m, null),
                }
            );

        act.Should().Throw<ArgumentException>().WithMessage("*duplicado*");
    }

    [Fact]
    public void ReplacePaymentSchedule_fuera_de_Draft_lanza()
    {
        // No copiar el hueco de PurchaseInvoice.ReplacePaymentSchedule (no valida estado) —
        // Ventas SIEMPRE exige EnsureDraft() aquí.
        var inv = CreateDraftWithLine();
        var payment = SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", inv.GrandTotal);
        inv.ReplacePayments(new[] { payment }, UserId);
        inv.Authorize(UserId);

        var act = () =>
            inv.ReplacePaymentSchedule(
                new List<(int, DateOnly, decimal, string?)> { (1, inv.IssueDate, inv.GrandTotal, null) }
            );

        act.Should().Throw<InvalidOperationException>();
    }
}
