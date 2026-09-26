using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — <see cref="SupplierCredit"/> generalizado: origen
/// PurchaseReturn XOR SupplierPayment, <see cref="SupplierCredit.SourceType"/> derivado, sin
/// eventos contables al crearse desde un pago, y anulación del anticipo solo si sigue íntegro.
/// </summary>
public sealed class SupplierCreditSupplierPaymentSourceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SupplierCredit FromPayment(Guid? paymentId = null, decimal amount = 20m) =>
        SupplierCredit.CreateFromSupplierPayment(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "usd",
            paymentId ?? Guid.NewGuid(),
            amount,
            UserId
        );

    private static SupplierCredit FromReturn() =>
        SupplierCredit.CreateFromReturn(TenantId, CompanyId, BranchId, SupplierId, "USD", Guid.NewGuid(), 30m, UserId);

    [Fact]
    public void CreateFromSupplierPayment_fija_origen_pago_monto_integro_y_no_levanta_eventos()
    {
        var paymentId = Guid.NewGuid();

        var credit = FromPayment(paymentId, 20m);

        credit.SourceType.Should().Be(SupplierCreditSourceType.SupplierPayment);
        credit.SourceSupplierPaymentId.Should().Be(paymentId);
        credit.SourcePurchaseReturnId.Should().BeNull();
        credit.OriginalAmount.Should().Be(20m);
        credit.AvailableAmount.Should().Be(20m);
        credit.IsIntact.Should().BeTrue();
        credit.CurrencyCode.Should().Be("USD");
        credit.DomainEvents.Should().BeEmpty("el único posting del anticipo es el del SupplierPayment");
    }

    [Fact]
    public void CreateFromReturn_sigue_funcionando_con_origen_devolucion()
    {
        var credit = FromReturn();

        credit.SourceType.Should().Be(SupplierCreditSourceType.PurchaseReturn);
        credit.SourcePurchaseReturnId.Should().NotBeNull();
        credit.SourceSupplierPaymentId.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateFromSupplierPayment_rechaza_monto_no_positivo(decimal amount)
    {
        var act = () => FromPayment(amount: amount);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromSupplierPayment_rechaza_pago_de_origen_vacio()
    {
        var act = () => FromPayment(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reversa_del_pago_con_anticipo_intacto_lo_deja_en_cero_con_movimiento_de_sistema()
    {
        var credit = FromPayment(amount: 20m);

        var movement = credit.RegisterSourcePaymentReversal(UserId, Guid.NewGuid(), "hash");

        movement.MovementType.Should().Be(SupplierCreditMovementType.SourcePaymentReversed);
        movement.Amount.Should().Be(20m);
        credit.AvailableAmount.Should().Be(0m);
        credit.OriginalAmount.Should().Be(20m);
    }

    [Fact]
    public void Reversa_del_pago_con_anticipo_aplicado_lanza()
    {
        var credit = FromPayment(amount: 20m);
        credit.ApplyToPayable(Guid.NewGuid(), 5m, UserId, Guid.NewGuid(), "h1");

        var act = () => credit.RegisterSourcePaymentReversal(UserId, Guid.NewGuid(), "h2");

        act.Should().Throw<InvalidOperationException>().WithMessage("*aplicado o reembolsado*");
    }

    [Fact]
    public void Reversa_del_pago_con_anticipo_reembolsado_lanza()
    {
        var credit = FromPayment(amount: 20m);
        credit.RegisterRefund(20m, UserId, Guid.NewGuid(), "h1");

        var act = () => credit.RegisterSourcePaymentReversal(UserId, Guid.NewGuid(), "h2");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Las_anulaciones_de_origen_no_se_cruzan_entre_tipos()
    {
        var fromPayment = FromPayment();
        var fromReturn = FromReturn();

        ((Action)(() => fromPayment.RegisterSourceReturnCancellation(UserId, Guid.NewGuid(), "h")))
            .Should().Throw<InvalidOperationException>();
        ((Action)(() => fromReturn.RegisterSourcePaymentReversal(UserId, Guid.NewGuid(), "h")))
            .Should().Throw<InvalidOperationException>();
    }
}
