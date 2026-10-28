using ERP.Application.Common;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Common;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

// Orchestration/domain only. Persistence, locks, HTTP permissions and posting await integrated E2E.
public sealed class ReverseCollectionCommandHandlerTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _company = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<ISalesReceivableRepository> _receivables = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ReverseCollectionCommandHandler Handler() => new(_payments.Object, _receivables.Object,
        _uow.Object, Mock.Of<ICurrentTenant>(t => t.TenantId == _tenant),
        Mock.Of<ICurrentCompany>(c => c.CompanyId == _company), Mock.Of<ICurrentUser>(u => u.UserId == _actor));

    private (Payment payment, SalesReceivable receivable) Seed(Guid? cash = null, Guid? bank = null, bool historical = false)
    {
        var customer = Guid.NewGuid();
        var ar = SalesReceivable.Create(_tenant, _company, Guid.NewGuid(), customer, 100m, _actor);
        var payment = Payment.Create(_tenant, _company, PaymentDirection.Collection, customer, 40m,
            new DateOnly(2026, 10, 28), null, "Caso 20", _actor, bank, cash);
        if (!historical) payment.BindClientRequest(new ClientRequestKey(Guid.NewGuid(), new string('a', 64)));
        payment.AddApplicationLine(ar.Id, null, 40m);
        payment.Apply(_actor);
        ar.RegisterCollection(40m, _actor);
        _payments.Setup(p => p.GetByIdForUpdateAsync(_tenant, _company, payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _receivables.Setup(r => r.GetByIdsForUpdateAsync(_tenant, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SalesReceivable> { [ar.Id] = ar });
        return (payment, ar);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("cash")]
    [InlineData("bank")]
    public async Task Anular_restaura_CxC_conserva_original_y_permite_cobro_correcto(string destination)
    {
        var (payment, ar) = Seed(destination == "cash" ? Guid.NewGuid() : null,
            destination == "bank" ? Guid.NewGuid() : null);
        var result = await Handler().Handle(new(payment.Id, " Error de registro "), default);
        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Reversed);
        payment.ReverseReason.Should().Be("Error de registro");
        payment.ReversedAtUtc.Should().NotBeNull();
        payment.UpdatedBy.Should().Be(_actor);
        payment.Lines.Should().ContainSingle();
        ar.PaidAmount.Should().Be(0m);
        ar.BalanceDue.Should().Be(100m);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        var register = new RegisterCollectionCommandHandler(_payments.Object, _receivables.Object,
            Mock.Of<ICompanyBankAccountRepository>(), Mock.Of<ICashRegisterRepository>(), _uow.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == _tenant),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == _company), Mock.Of<ICurrentUser>(u => u.UserId == _actor));
        var corrected = await register.Handle(new RegisterCollectionCommand(payment.PartnerId, 25m,
            new DateOnly(2026, 10, 28), null, "Cobro correcto",
            [new(ar.Id, null, 25m)], ClientRequestId: Guid.NewGuid()), default);
        corrected.IsSuccess.Should().BeTrue();
        corrected.Value!.Id.Should().NotBe(payment.Id);
        ar.PaidAmount.Should().Be(25m);
        ar.BalanceDue.Should().Be(75m);
    }

    [Fact]
    public async Task Duplicado_se_rechaza_sin_segundo_reverso()
    {
        var (payment, ar) = Seed();
        await Handler().Handle(new(payment.Id, "Error"), default);
        (await Handler().Handle(new(payment.Id, "Otro motivo"), default)).IsSuccess.Should().BeFalse();
        ar.PaidAmount.Should().Be(0m);
        _payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Movimiento_operativo_vinculado_bloquea_sin_mutar()
    {
        var (payment, ar) = Seed(cash: Guid.NewGuid());
        _payments.Setup(p => p.HasOperationalReferencesAsync(_tenant, _company, payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        (await Handler().Handle(new(payment.Id, "Error"), default)).IsSuccess.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.Applied);
        ar.PaidAmount.Should().Be(40m);
        _payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Historico_con_destino_sin_procedencia_demostrable_bloquea(bool cash)
    {
        var (payment, ar) = Seed(cash ? Guid.NewGuid() : null, cash ? null : Guid.NewGuid(), historical: true);
        (await Handler().Handle(new(payment.Id, "Error"), default)).IsSuccess.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.Applied);
        ar.PaidAmount.Should().Be(40m);
    }

    [Fact]
    public async Task Historico_sin_destino_ni_movimientos_permite_correccion_documental()
    {
        var (payment, ar) = Seed(historical: true);
        (await Handler().Handle(new(payment.Id, "Error"), default)).IsSuccess.Should().BeTrue();
        ar.PaidAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Scope_no_encontrado_no_escribe()
    {
        (await Handler().Handle(new(Guid.NewGuid(), "Error"), default)).IsSuccess.Should().BeFalse();
        _payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Operacion_posterior_incompatible_bloquea_antes_de_mutar_el_original()
    {
        var (payment, ar) = Seed();
        ar.ReverseCollection(40m, _actor);
        ar.Cancel(_actor);
        (await Handler().Handle(new(payment.Id, "Error"), default)).IsSuccess.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.Applied);
        _payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CxC_fuera_de_empresa_bloquea_antes_de_mutar()
    {
        var (payment, _) = Seed();
        var foreign = SalesReceivable.Create(_tenant, Guid.NewGuid(), Guid.NewGuid(), payment.PartnerId, 100m, _actor);
        _receivables.Setup(r => r.GetByIdsForUpdateAsync(_tenant, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SalesReceivable> { [payment.Lines.Single().ReceivableId!.Value] = foreign });
        (await Handler().Handle(new(payment.Id, "Error"), default)).IsSuccess.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.Applied);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Motivo_obligatorio(string reason) =>
        (await new ReverseCollectionCommandValidator().ValidateAsync(new ReverseCollectionCommand(Guid.NewGuid(), reason)))
            .IsValid.Should().BeFalse();
}
