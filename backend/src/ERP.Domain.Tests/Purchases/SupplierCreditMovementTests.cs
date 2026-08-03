using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// <see cref="SupplierCreditMovement.Create"/> es <c>internal</c> — única API pública de creación
/// es <see cref="SupplierCredit"/> (<c>ApplyToPayable</c>/<c>RegisterRefund</c>/
/// <c>ReverseApplication</c>/<c>ReverseRefund</c>/<c>RegisterSourceReturnCancellation</c>), mismo
/// criterio que <c>PurchaseReturnDetail.Freeze</c>. Estas pruebas verifican los <c>CHECK</c>
/// combinados de §7.5/§13.3 a través de esa API pública.
/// </summary>
public sealed class SupplierCreditMovementTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid SourceReturnId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TargetPayableId = Guid.NewGuid();

    private static SupplierCredit CreateCredit(decimal originalAmount = 150m) =>
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

    [Fact]
    public void Application_exige_TargetPurchasePayableId()
    {
        var credit = CreateCredit();

        var act = () => credit.ApplyToPayable(Guid.Empty, 50m, UserId, Guid.NewGuid(), "hash-001");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Refund_no_admite_TargetPurchasePayableId()
    {
        var credit = CreateCredit();

        var movement = credit.RegisterRefund(50m, UserId, Guid.NewGuid(), "hash-002");

        movement.TargetPurchasePayableId.Should().BeNull();
    }

    [Fact]
    public void SourceReturnCancelled_no_admite_TargetPurchasePayableId()
    {
        var credit = CreateCredit();

        var movement = credit.RegisterSourceReturnCancellation(UserId, Guid.NewGuid(), "hash-003");

        movement.TargetPurchasePayableId.Should().BeNull();
        movement.MovementType.Should().Be(SupplierCreditMovementType.SourceReturnCancelled);
    }

    [Fact]
    public void ReversalOfApplication_exige_ReversalOfMovementId_y_lo_setea()
    {
        var credit = CreateCredit();
        var applied = credit.ApplyToPayable(TargetPayableId, 50m, UserId, Guid.NewGuid(), "hash-004");

        var reversal = credit.ReverseApplication(applied.Id, UserId, Guid.NewGuid(), "hash-005");

        reversal.ReversalOfMovementId.Should().Be(applied.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Amount_debe_ser_mayor_a_cero(decimal amount)
    {
        var credit = CreateCredit();

        var act = () => credit.ApplyToPayable(TargetPayableId, amount, UserId, Guid.NewGuid(), "hash-006");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_mismo_movimiento_no_puede_revertirse_dos_veces_UNIQUE_ReversalOfMovementId()
    {
        var credit = CreateCredit();
        var applied = credit.ApplyToPayable(TargetPayableId, 50m, UserId, Guid.NewGuid(), "hash-007");
        credit.ReverseApplication(applied.Id, UserId, Guid.NewGuid(), "hash-008");

        var act = () => credit.ReverseApplication(applied.Id, UserId, Guid.NewGuid(), "hash-009");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── ClientRequestId / RequestPayloadHash (§7.5, §16.2 — Enmienda DOMAIN_AMENDMENT_01) ──

    [Fact]
    public void Movimiento_conserva_ClientRequestId_y_RequestPayloadHash_sin_transformacion()
    {
        var credit = CreateCredit();
        var clientRequestId = Guid.NewGuid();

        var movement = credit.ApplyToPayable(
            TargetPayableId,
            50m,
            UserId,
            clientRequestId,
            "hash-payload-010"
        );

        movement.ClientRequestId.Should().Be(clientRequestId);
        movement.RequestPayloadHash.Should().Be("hash-payload-010");
    }

    [Fact]
    public void Movimiento_rechaza_ClientRequestId_vacio()
    {
        var credit = CreateCredit();

        var act = () =>
            credit.ApplyToPayable(TargetPayableId, 50m, UserId, Guid.Empty, "hash-011");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Movimiento_rechaza_RequestPayloadHash_invalido(string? hash)
    {
        var credit = CreateCredit();

        var act = () =>
            credit.ApplyToPayable(TargetPayableId, 50m, UserId, Guid.NewGuid(), hash!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClientRequestId_y_RequestPayloadHash_no_exponen_mutador_publico()
    {
        var methodNames = typeof(SupplierCreditMovement)
            .GetMethods()
            .Select(m => m.Name)
            .ToList();
        methodNames.Should().NotContain("SetClientRequestId");
        methodNames.Should().NotContain("SetRequestPayloadHash");
        typeof(SupplierCreditMovement)
            .GetProperty(nameof(SupplierCreditMovement.ClientRequestId))!
            .SetMethod!.IsPublic.Should()
            .BeFalse();
        typeof(SupplierCreditMovement)
            .GetProperty(nameof(SupplierCreditMovement.RequestPayloadHash))!
            .SetMethod!.IsPublic.Should()
            .BeFalse();
    }
}
