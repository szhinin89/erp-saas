using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Finance;

/// <summary>Fase 5.6.3 — Payment: agregado raíz de liquidación (Fase 5.5.5.2).</summary>
public sealed class PaymentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid PartnerId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();

    private static Payment CreateCollection(decimal amount = 100m) =>
        Payment.Create(
            TenantId,
            CompanyId,
            PaymentDirection.Collection,
            PartnerId,
            amount,
            new DateOnly(2026, 7, 25),
            paymentMethodId: null,
            reference: null,
            CreatedBy
        );

    private static Payment CreatePayment(decimal amount = 100m) =>
        Payment.Create(
            TenantId,
            CompanyId,
            PaymentDirection.Payment,
            PartnerId,
            amount,
            new DateOnly(2026, 7, 25),
            paymentMethodId: null,
            reference: null,
            CreatedBy
        );

    // ── Create ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_valido_queda_en_Draft_sin_lineas()
    {
        var payment = CreateCollection();

        payment.Status.Should().Be(PaymentStatus.Draft);
        payment.Direction.Should().Be(PaymentDirection.Collection);
        payment.PartnerId.Should().Be(PartnerId);
        payment.Amount.Should().Be(100m);
        payment.Lines.Should().BeEmpty();
        payment.AppliedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_rechaza_amount_menor_o_igual_a_cero()
    {
        var actZero = () =>
            Payment.Create(
                TenantId,
                CompanyId,
                PaymentDirection.Collection,
                PartnerId,
                0m,
                new DateOnly(2026, 7, 25),
                null,
                null,
                CreatedBy
            );
        var actNegative = () =>
            Payment.Create(
                TenantId,
                CompanyId,
                PaymentDirection.Collection,
                PartnerId,
                -10m,
                new DateOnly(2026, 7, 25),
                null,
                null,
                CreatedBy
            );

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_partnerId_vacio()
    {
        var act = () =>
            Payment.Create(
                TenantId,
                CompanyId,
                PaymentDirection.Collection,
                Guid.Empty,
                100m,
                new DateOnly(2026, 7, 25),
                null,
                null,
                CreatedBy
            );

        act.Should().Throw<ArgumentException>();
    }

    // ── AddApplicationLine ──────────────────────────────────────────────

    [Fact]
    public void AddApplicationLine_en_direccion_Collection_asigna_ReceivableId()
    {
        var payment = CreateCollection();

        payment.AddApplicationLine(DocumentId, null, 100m);

        payment.Lines.Should().ContainSingle();
        payment.Lines.Single().ReceivableId.Should().Be(DocumentId);
        payment.Lines.Single().PayableId.Should().BeNull();
    }

    [Fact]
    public void AddApplicationLine_en_direccion_Payment_asigna_PayableId()
    {
        var payment = CreatePayment();

        payment.AddApplicationLine(DocumentId, null, 100m);

        payment.Lines.Should().ContainSingle();
        payment.Lines.Single().PayableId.Should().Be(DocumentId);
        payment.Lines.Single().ReceivableId.Should().BeNull();
    }

    [Fact]
    public void AddApplicationLine_rechaza_agregar_fuera_de_Draft()
    {
        var payment = CreateCollection();
        payment.AddApplicationLine(DocumentId, null, 100m);
        payment.Apply(CreatedBy);

        var act = () => payment.AddApplicationLine(Guid.NewGuid(), null, 1m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    // ── Apply ───────────────────────────────────────────────────────────

    [Fact]
    public void Apply_balanceado_cambia_a_Applied_y_publica_CollectionAppliedEvent()
    {
        var payment = CreateCollection();
        payment.AddApplicationLine(DocumentId, null, 100m);

        payment.Apply(CreatedBy);

        payment.Status.Should().Be(PaymentStatus.Applied);
        payment.AppliedAtUtc.Should().NotBeNull();
        payment.DomainEvents.Should().ContainSingle(e => e is CollectionAppliedEvent);
    }

    [Fact]
    public void Apply_balanceado_en_direccion_Payment_publica_SupplierPaymentAppliedEvent()
    {
        var payment = CreatePayment();
        payment.AddApplicationLine(DocumentId, null, 100m);

        payment.Apply(CreatedBy);

        payment.DomainEvents.Should().ContainSingle(e => e is SupplierPaymentAppliedEvent);
    }

    [Fact]
    public void Apply_sin_lineas_lanza_y_no_cambia_estado()
    {
        var payment = CreateCollection();

        var act = () => payment.Apply(CreatedBy);

        act.Should().Throw<InvalidOperationException>().WithMessage("*al menos una línea*");
        payment.Status.Should().Be(PaymentStatus.Draft);
    }

    [Fact]
    public void Apply_desbalanceado_lanza_y_no_cambia_estado()
    {
        var payment = CreateCollection(100m);
        payment.AddApplicationLine(DocumentId, null, 40m);

        var act = () => payment.Apply(CreatedBy);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no está balanceado*");
        payment.Status.Should().Be(PaymentStatus.Draft);
        payment.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Segundo_Apply_es_rechazado()
    {
        var payment = CreateCollection();
        payment.AddApplicationLine(DocumentId, null, 100m);
        payment.Apply(CreatedBy);

        var act = () => payment.Apply(CreatedBy);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
        payment.Status.Should().Be(PaymentStatus.Applied);
    }

    // ── Reverse ─────────────────────────────────────────────────────────

    [Fact]
    public void Reverse_correcto_cambia_a_Reversed_y_publica_evento()
    {
        var payment = CreateCollection();
        payment.AddApplicationLine(DocumentId, null, 100m);
        payment.Apply(CreatedBy);

        payment.Reverse(CreatedBy, "Error de digitación");

        payment.Status.Should().Be(PaymentStatus.Reversed);
        payment.ReversedAtUtc.Should().NotBeNull();
        payment.ReverseReason.Should().Be("Error de digitación");
        payment.DomainEvents.Should().ContainSingle(e => e is CollectionReversedEvent);
    }

    [Fact]
    public void Reverse_sobre_Draft_es_rechazado()
    {
        var payment = CreateCollection();
        payment.AddApplicationLine(DocumentId, null, 100m);

        var act = () => payment.Reverse(CreatedBy, "Motivo");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Applied*");
    }

    [Fact]
    public void Segundo_Reverse_es_rechazado()
    {
        var payment = CreateCollection();
        payment.AddApplicationLine(DocumentId, null, 100m);
        payment.Apply(CreatedBy);
        payment.Reverse(CreatedBy, "Primer reverso");

        var act = () => payment.Reverse(CreatedBy, "Segundo intento");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Applied*");
        payment.ReverseReason.Should().Be("Primer reverso");
    }

    [Fact]
    public void Reverse_rechaza_motivo_vacio()
    {
        var payment = CreateCollection();
        payment.AddApplicationLine(DocumentId, null, 100m);
        payment.Apply(CreatedBy);

        var act = () => payment.Reverse(CreatedBy, "   ");

        act.Should().Throw<ArgumentException>();
        payment.Status.Should().Be(PaymentStatus.Applied);
    }
}
