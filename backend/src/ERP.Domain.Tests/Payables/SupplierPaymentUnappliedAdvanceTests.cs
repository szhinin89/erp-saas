using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Payables;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — invariantes de <see cref="SupplierPayment.Create"/>
/// con remanente no aplicado: Σ aplicaciones ≤ total, AppliedAmount/UnappliedAmount derivados,
/// confirmación explícita obligatoria, pago sin CxP solo con política de empresa, matriz medio↔cuota
/// que no obliga a distribuir el remanente.
/// </summary>
public sealed class SupplierPaymentUnappliedAdvanceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly DateOnly PaymentDate = new(2026, 8, 28);

    private static SupplierPayment Create(
        decimal total,
        IReadOnlyList<SupplierPaymentMethodLineInput> methods,
        IReadOnlyList<SupplierPaymentApplicationLineInput> applications,
        IReadOnlyList<SupplierPaymentAllocationInput> allocations,
        bool confirm,
        bool allowWithoutPayable = false
    ) =>
        SupplierPayment.Create(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            PaymentDate,
            total,
            "00000001",
            null,
            methods,
            applications,
            allocations,
            CreatedBy,
            confirm,
            allowWithoutPayable
        );

    private static SupplierPaymentMethodLineInput Bank(decimal amount) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, amount, TransactionDate: PaymentDate);

    private static SupplierPaymentMethodLineInput Cash(decimal amount) =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), amount);

    [Fact]
    public void Pago_exacto_contra_CxP_no_deja_remanente_ni_requiere_confirmacion()
    {
        var payment = Create(
            180m,
            [Bank(180m)],
            [new(Guid.NewGuid(), 180m)],
            [new(0, 0, 180m)],
            confirm: false
        );

        payment.AppliedAmount.Should().Be(180m);
        payment.UnappliedAmount.Should().Be(0m);
    }

    [Fact]
    public void Pago_parcial_de_una_cuota_es_valido_sin_remanente()
    {
        // Cuota con saldo 180, pago 100 aplicado completo a la cuota (pago parcial de la deuda).
        var payment = Create(100m, [Bank(100m)], [new(Guid.NewGuid(), 100m)], [new(0, 0, 100m)], confirm: false);

        payment.UnappliedAmount.Should().Be(0m);
    }

    [Fact]
    public void Excedente_confirmado_deriva_Applied_y_Unapplied_exactos_y_los_publica_en_el_evento()
    {
        var payment = Create(200m, [Bank(200m)], [new(Guid.NewGuid(), 180m)], [new(0, 0, 180m)], confirm: true);

        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payment.AppliedAmount.Should().Be(180m);
        payment.UnappliedAmount.Should().Be(20m);
        var evt = payment.DomainEvents.OfType<SupplierPaymentConfirmedEvent>().Single();
        evt.TotalAmount.Should().Be(200m);
        evt.AppliedAmount.Should().Be(180m);
        evt.UnappliedAmount.Should().Be(20m);
    }

    [Fact]
    public void Excedente_sin_confirmacion_explicita_lanza()
    {
        var act = () => Create(200m, [Bank(200m)], [new(Guid.NewGuid(), 180m)], [new(0, 0, 180m)], confirm: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*20.00 sin aplicar*");
    }

    [Fact]
    public void Aplicaciones_mayores_al_total_lanzan()
    {
        var act = () => Create(100m, [Bank(100m)], [new(Guid.NewGuid(), 120m)], [new(0, 0, 100m)], confirm: true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Sin_aplicaciones_y_sin_politica_de_empresa_lanza_aunque_se_confirme()
    {
        var act = () => Create(200m, [Bank(200m)], [], [], confirm: true, allowWithoutPayable: false);

        act.Should().Throw<ArgumentException>().WithMessage("*sin una cuenta por pagar*");
    }

    [Fact]
    public void Sin_aplicaciones_con_politica_y_confirmado_deja_todo_el_total_sin_aplicar()
    {
        var payment = Create(200m, [Bank(200m)], [], [], confirm: true, allowWithoutPayable: true);

        payment.AppliedAmount.Should().Be(0m);
        payment.UnappliedAmount.Should().Be(200m);
        payment.AllocationLines.Should().BeEmpty();
        payment.DomainEvents.OfType<SupplierPaymentConfirmedEvent>().Single().AppliedAmount.Should().Be(0m);
    }

    [Fact]
    public void Sin_aplicaciones_con_politica_pero_sin_confirmar_lanza()
    {
        var act = () => Create(200m, [Bank(200m)], [], [], confirm: false, allowWithoutPayable: true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Sin_aplicaciones_no_admite_distribuciones()
    {
        var act = () => Create(200m, [Bank(200m)], [], [new(0, 0, 50m)], confirm: true, allowWithoutPayable: true);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Medio_distribuido_por_encima_de_su_monto_lanza()
    {
        // Caja 50 + banco 150 = 200; se intenta distribuir 80 desde caja.
        var act = () =>
            Create(
                200m,
                [Cash(50m), Bank(150m)],
                [new(Guid.NewGuid(), 180m)],
                [new(0, 0, 80m), new(1, 0, 100m)],
                confirm: true
            );

        act.Should().Throw<InvalidOperationException>().WithMessage("*por encima de su monto*");
    }

    [Fact]
    public void Pago_mixto_con_remanente_la_matriz_cubre_las_cuotas_y_el_remanente_queda_en_el_medio()
    {
        // Caja 100 + banco 250 = 350; cuota 300 = caja 100 + banco 200; remanente 50 del banco.
        var payment = Create(
            350m,
            [Cash(100m), Bank(250m)],
            [new(Guid.NewGuid(), 300m)],
            [new(0, 0, 100m), new(1, 0, 200m)],
            confirm: true
        );

        payment.UnappliedAmount.Should().Be(50m);
        var bank = payment.MethodLines[1];
        (bank.Amount - payment.AllocationLines.Where(a => a.SupplierPaymentMethodLineId == bank.Id).Sum(a => a.Amount))
            .Should().Be(50m);
    }

    [Fact]
    public void Reversa_publica_Applied_del_pago_original()
    {
        var payment = Create(200m, [Bank(200m)], [new(Guid.NewGuid(), 180m)], [new(0, 0, 180m)], confirm: true);

        payment.Reverse("Error", CreatedBy, DateTime.UtcNow, bankReversalReason: SupplierPaymentBankReversalReason.NotExecuted);

        var evt = payment.DomainEvents.OfType<SupplierPaymentReversedEvent>().Single();
        evt.AppliedAmount.Should().Be(180m);
        evt.UnappliedAmount.Should().Be(20m);
    }
}
