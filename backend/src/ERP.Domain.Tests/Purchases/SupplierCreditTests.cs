using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

public sealed class SupplierCreditTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid SourceReturnId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TargetPayableId = Guid.NewGuid();

    private static SupplierCredit Create(decimal originalAmount = 150m) =>
        SupplierCredit.CreateFromReturn(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "USD",
            SourceReturnId,
            originalAmount,
            UserId
        );

    // ── CreateFromReturn ──────────────────────────────────────────────

    [Fact]
    public void CreateFromReturn_con_datos_validos_queda_abierto_con_saldo_completo()
    {
        var credit = Create(150m);

        credit.OriginalAmount.Should().Be(150m);
        credit.AvailableAmount.Should().Be(150m);
        credit.IsOpen.Should().BeTrue();
        credit.BranchId.Should().Be(BranchId);
        credit.SourcePurchaseReturnId.Should().Be(SourceReturnId);
        credit.Movements.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CreateFromReturn_rechaza_monto_no_positivo(decimal amount)
    {
        var act = () => Create(amount);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromReturn_rechaza_BranchId_vacio()
    {
        var act = () =>
            SupplierCredit.CreateFromReturn(
                TenantId,
                CompanyId,
                Guid.Empty,
                SupplierId,
                "USD",
                SourceReturnId,
                150m,
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    // ── ApplyToPayable / ReverseApplication ───────────────────────────

    [Fact]
    public void ApplyToPayable_reduce_AvailableAmount_y_crea_movimiento_Application()
    {
        var credit = Create(150m);

        var movement = credit.ApplyToPayable(
            TargetPayableId,
            100m,
            UserId,
            Guid.NewGuid(),
            "hash-a1"
        );

        credit.AvailableAmount.Should().Be(50m);
        movement.MovementType.Should().Be(SupplierCreditMovementType.Application);
        movement.TargetPurchasePayableId.Should().Be(TargetPayableId);
        credit.Movements.Should().ContainSingle();
        credit.DomainEvents.Should().ContainSingle(e => e is SupplierCreditAppliedEvent);
    }

    [Fact]
    public void ApplyToPayable_excede_disponible_lanza()
    {
        var credit = Create(150m);

        var act = () =>
            credit.ApplyToPayable(TargetPayableId, 200m, UserId, Guid.NewGuid(), "hash-a2");

        act.Should().Throw<InvalidOperationException>();
        credit.AvailableAmount.Should().Be(150m);
    }

    [Fact]
    public void ReverseApplication_restablece_AvailableAmount()
    {
        var credit = Create(150m);
        var applied = credit.ApplyToPayable(
            TargetPayableId,
            100m,
            UserId,
            Guid.NewGuid(),
            "hash-a3"
        );

        var reversal = credit.ReverseApplication(applied.Id, UserId, Guid.NewGuid(), "hash-a4");

        credit.AvailableAmount.Should().Be(150m);
        reversal.MovementType.Should().Be(SupplierCreditMovementType.ReversalOfApplication);
        reversal.ReversalOfMovementId.Should().Be(applied.Id);
        credit
            .DomainEvents.Should()
            .ContainSingle(e => e is SupplierCreditApplicationReversedEvent);
    }

    [Fact]
    public void ReverseApplication_dos_veces_lanza()
    {
        var credit = Create(150m);
        var applied = credit.ApplyToPayable(
            TargetPayableId,
            100m,
            UserId,
            Guid.NewGuid(),
            "hash-a5"
        );
        credit.ReverseApplication(applied.Id, UserId, Guid.NewGuid(), "hash-a6");

        var act = () => credit.ReverseApplication(applied.Id, UserId, Guid.NewGuid(), "hash-a7");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReverseApplication_con_movimiento_inexistente_lanza()
    {
        var credit = Create(150m);

        var act = () =>
            credit.ReverseApplication(Guid.NewGuid(), UserId, Guid.NewGuid(), "hash-a8");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── RegisterRefund / ReverseRefund ────────────────────────────────

    [Fact]
    public void RegisterRefund_reduce_AvailableAmount_y_crea_movimiento_Refund()
    {
        var credit = Create(150m);

        var movement = credit.RegisterRefund(150m, UserId, Guid.NewGuid(), "hash-r1");

        credit.AvailableAmount.Should().Be(0m);
        credit.IsOpen.Should().BeFalse();
        movement.MovementType.Should().Be(SupplierCreditMovementType.Refund);
        movement.TargetPurchasePayableId.Should().BeNull();
        credit.DomainEvents.Should().ContainSingle(e => e is SupplierCreditRefundedEvent);
    }

    [Fact]
    public void RegisterRefund_excede_disponible_lanza()
    {
        var credit = Create(150m);

        var act = () => credit.RegisterRefund(151m, UserId, Guid.NewGuid(), "hash-r2");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReverseRefund_restablece_AvailableAmount()
    {
        var credit = Create(150m);
        var refund = credit.RegisterRefund(150m, UserId, Guid.NewGuid(), "hash-r3");

        var reversal = credit.ReverseRefund(refund.Id, UserId, Guid.NewGuid(), "hash-r4");

        credit.AvailableAmount.Should().Be(150m);
        reversal.MovementType.Should().Be(SupplierCreditMovementType.ReversalOfRefund);
        credit.DomainEvents.Should().ContainSingle(e => e is SupplierCreditRefundReversedEvent);
    }

    // ── RegisterSourceReturnCancellation (5 tipos de movimiento — §13.5) ──

    [Fact]
    public void RegisterSourceReturnCancellation_con_credito_integro_deja_AvailableAmount_en_cero()
    {
        var credit = Create(150m);

        var movement = credit.RegisterSourceReturnCancellation(UserId, Guid.NewGuid(), "hash-s1");

        credit.AvailableAmount.Should().Be(0m);
        movement.MovementType.Should().Be(SupplierCreditMovementType.SourceReturnCancelled);
        movement.Amount.Should().Be(150m);
    }

    [Fact]
    public void RegisterSourceReturnCancellation_con_credito_ya_aplicado_lanza()
    {
        var credit = Create(150m);
        credit.ApplyToPayable(TargetPayableId, 50m, UserId, Guid.NewGuid(), "hash-s2");

        var act = () => credit.RegisterSourceReturnCancellation(UserId, Guid.NewGuid(), "hash-s3");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Fórmula de §13.5 con los 5 tipos de movimiento combinados ─────

    [Fact]
    public void AvailableAmount_se_recalcula_correctamente_combinando_los_5_tipos_de_movimiento()
    {
        var credit = Create(1000m);

        var applied = credit.ApplyToPayable(
            TargetPayableId,
            300m,
            UserId,
            Guid.NewGuid(),
            "hash-c1"
        ); // 700
        var refunded = credit.RegisterRefund(200m, UserId, Guid.NewGuid(), "hash-c2"); // 500
        credit.ReverseApplication(applied.Id, UserId, Guid.NewGuid(), "hash-c3"); // 800
        credit.ReverseRefund(refunded.Id, UserId, Guid.NewGuid(), "hash-c4"); // 1000

        credit.AvailableAmount.Should().Be(1000m);

        credit.RegisterSourceReturnCancellation(UserId, Guid.NewGuid(), "hash-c5"); // 0
        credit.AvailableAmount.Should().Be(0m);
    }
}
