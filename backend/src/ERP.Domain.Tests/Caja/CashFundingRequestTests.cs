using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Caja;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — invariantes de <see cref="CashFundingRequest"/>:
/// creación Pending válida, Pending como único estado mutable, estados terminales sin reutilización,
/// SupplierPaymentId solo en Fulfilled, motivo obligatorio en Rejected/Cancelled.
/// </summary>
public sealed class CashFundingRequestTests
{
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Cashier = Guid.NewGuid();

    private static CashFundingRequest Pending(decimal total = 200m, decimal cash = 80m) =>
        CashFundingRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            total,
            cash,
            Requester,
            "{\"supplierId\":\"x\"}",
            1,
            new string('A', 64),
            Guid.NewGuid()
        );

    [Fact]
    public void Creacion_valida_queda_Pending_sin_datos_de_resolucion()
    {
        var request = Pending();

        request.Status.Should().Be(CashFundingRequestStatus.Pending);
        request.IsPending.Should().BeTrue();
        request.RequestedByUserId.Should().Be(Requester);
        request.RequestedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        (request.ResolvedByUserId, request.ResolvedAtUtc, request.ResolutionReason, request.SupplierPaymentId)
            .Should().Be(((Guid?)null, (DateTime?)null, (string?)null, (Guid?)null));
    }

    [Theory]
    [InlineData(200, 0)]
    [InlineData(200, -5)]
    [InlineData(50, 80)]
    public void Montos_invalidos_se_rechazan(decimal total, decimal cash)
    {
        var act = () => Pending(total, cash);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Total_igual_al_efectivo_es_valido_pago_solo_en_caja()
    {
        Pending(80m, 80m).CashAmount.Should().Be(80m);
    }

    [Fact]
    public void Caja_y_sesion_objetivo_son_obligatorias()
    {
        var act = () => CashFundingRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
            100m, 50m, Requester, "{}", 1, "H", Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("cashRegisterId");

        var act2 = () => CashFundingRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(),
            100m, 50m, Requester, "{}", 1, "H", Guid.NewGuid());
        act2.Should().Throw<ArgumentException>().WithParameterName("cashSessionId");
    }

    [Fact]
    public void Payload_version_y_hash_son_obligatorios()
    {
        FluentActions.Invoking(() => CashFundingRequest.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                100m, 50m, Requester, " ", 1, "H", Guid.NewGuid()))
            .Should().Throw<ArgumentException>().WithParameterName("paymentPayload");
        FluentActions.Invoking(() => CashFundingRequest.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                100m, 50m, Requester, "{}", 0, "H", Guid.NewGuid()))
            .Should().Throw<ArgumentException>().WithParameterName("payloadVersion");
        FluentActions.Invoking(() => CashFundingRequest.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                100m, 50m, Requester, "{}", 1, "", Guid.NewGuid()))
            .Should().Throw<ArgumentException>().WithParameterName("payloadHash");
    }

    [Fact]
    public void Fulfill_registra_cajero_y_pago_y_es_terminal()
    {
        var request = Pending();
        var paymentId = Guid.NewGuid();

        request.Fulfill(Cashier, paymentId);

        request.Status.Should().Be(CashFundingRequestStatus.Fulfilled);
        request.ResolvedByUserId.Should().Be(Cashier);
        request.ResolvedAtUtc.Should().NotBeNull();
        request.SupplierPaymentId.Should().Be(paymentId);
        request.ResolutionReason.Should().BeNull();

        FluentActions.Invoking(() => request.Fulfill(Cashier, Guid.NewGuid()))
            .Should().Throw<DomainRuleViolationException>("una solicitud atendida no puede reutilizarse");
        FluentActions.Invoking(() => request.Reject(Cashier, "x")).Should().Throw<DomainRuleViolationException>();
        FluentActions.Invoking(() => request.Cancel(Requester, "x")).Should().Throw<DomainRuleViolationException>();
        request.SupplierPaymentId.Should().Be(paymentId, "el pago original nunca se reemplaza");
    }

    [Fact]
    public void Fulfill_exige_cajero_y_pago()
    {
        FluentActions.Invoking(() => Pending().Fulfill(Guid.Empty, Guid.NewGuid())).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Pending().Fulfill(Cashier, Guid.Empty)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reject_exige_motivo_no_tiene_pago_y_es_terminal()
    {
        FluentActions.Invoking(() => Pending().Reject(Cashier, "  ")).Should().Throw<ArgumentException>();

        var request = Pending();
        request.Reject(Cashier, "  Sin efectivo suficiente  ");

        request.Status.Should().Be(CashFundingRequestStatus.Rejected);
        (request.ResolvedByUserId, request.ResolutionReason, request.SupplierPaymentId)
            .Should().Be(((Guid?)Cashier, "Sin efectivo suficiente", (Guid?)null));
        FluentActions.Invoking(() => request.Fulfill(Cashier, Guid.NewGuid())).Should().Throw<DomainRuleViolationException>();
        FluentActions.Invoking(() => request.Cancel(Requester, "x")).Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Cancel_exige_motivo_no_tiene_pago_y_es_terminal()
    {
        FluentActions.Invoking(() => Pending().Cancel(Requester, "")).Should().Throw<ArgumentException>();

        var request = Pending();
        request.Cancel(Requester, "Ya no se necesita");

        request.Status.Should().Be(CashFundingRequestStatus.Cancelled);
        (request.ResolvedByUserId, request.ResolutionReason, request.SupplierPaymentId)
            .Should().Be(((Guid?)Requester, "Ya no se necesita", (Guid?)null));
        FluentActions.Invoking(() => request.Fulfill(Cashier, Guid.NewGuid())).Should().Throw<DomainRuleViolationException>();
        FluentActions.Invoking(() => request.Reject(Cashier, "x")).Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Motivo_mayor_al_maximo_se_rechaza()
    {
        FluentActions.Invoking(() => Pending().Reject(Cashier, new string('x', CashFundingRequest.ResolutionReasonMaxLen + 1)))
            .Should().Throw<ArgumentException>();
    }
}
