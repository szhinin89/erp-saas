using ERP.Application.Common;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — cobertura de orquestación de
/// <see cref="RegisterPaymentCommandHandler"/>, simétrica a
/// <c>RegisterCollectionCommandHandlerTests</c> (AR). Las reglas de negocio puras (balance,
/// límites, retención) ya viven en <c>PurchasePayableTests</c>/<c>PaymentTests</c> (Domain).
/// </summary>
public sealed class RegisterPaymentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseId = Guid.NewGuid();

    private static PurchasePayable CreatePayable(decimal amount = 100m) =>
        PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, amount, UserId);

    private static (
        Mock<IPaymentRepository> payments,
        Mock<IPurchasePayableRepository> payables,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company,
        Mock<ICurrentUser> user
    ) BuildMocks()
    {
        var payments = new Mock<IPaymentRepository>();
        var payables = new Mock<IPurchasePayableRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var user = new Mock<ICurrentUser>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        user.Setup(u => u.UserId).Returns(UserId);

        return (payments, payables, tenant, company, user);
    }

    private static RegisterPaymentCommandHandler BuildHandler(
        Mock<IPaymentRepository> payments,
        Mock<IPurchasePayableRepository> payables,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company,
        Mock<ICurrentUser> user
    ) => new(payments.Object, payables.Object, tenant.Object, company.Object, user.Object);

    [Fact]
    public async Task Pago_valido_aplica_el_pago_y_actualiza_el_saldo_de_la_CxP()
    {
        var (payments, payables, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        var handler = BuildHandler(payments, payables, tenant, company, user);
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            60m,
            new DateOnly(2026, 7, 30),
            null,
            "REF-1",
            new[] { new PaymentApplicationLineInput(payable.Id, null, 60m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payable.PaidAmount.Should().Be(60m);
        payable.BalanceDue.Should().Be(40m);
        payments.Verify(p => p.AddAsync(It.IsAny<Domain.Modules.Finance.Entities.Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pago_valido_publica_SupplierPaymentAppliedEvent_en_el_Payment_persistido()
    {
        var (payments, payables, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        Domain.Modules.Finance.Entities.Payment? captured = null;
        payments
            .Setup(p => p.AddAsync(It.IsAny<Domain.Modules.Finance.Entities.Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Modules.Finance.Entities.Payment, CancellationToken>((p, _) => captured = p)
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(payments, payables, tenant, company, user);
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            100m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, null, 100m) }
        );

        await handler.Handle(cmd, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DomainEvents.Should().ContainSingle(e => e is SupplierPaymentAppliedEvent);
    }

    [Fact]
    public async Task Pago_con_InstallmentId_lo_propaga_a_la_linea_de_aplicacion_del_pago()
    {
        var (payments, payables, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(300m);
        var schedule = new List<Domain.Modules.Purchases.Entities.PurchasePaymentSchedule>
        {
            Domain.Modules.Purchases.Entities.PurchasePaymentSchedule.Create(
                PurchaseId,
                TenantId,
                1,
                new DateOnly(2026, 8, 30),
                150m
            ),
            Domain.Modules.Purchases.Entities.PurchasePaymentSchedule.Create(
                PurchaseId,
                TenantId,
                2,
                new DateOnly(2026, 9, 30),
                150m
            ),
        };
        payable.GenerateInstallments(schedule);
        var installmentId = payable.Installments[0].Id;
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        Domain.Modules.Finance.Entities.Payment? captured = null;
        payments
            .Setup(p => p.AddAsync(It.IsAny<Domain.Modules.Finance.Entities.Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Modules.Finance.Entities.Payment, CancellationToken>((p, _) => captured = p)
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(payments, payables, tenant, company, user);
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            150m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, installmentId, 150m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.Lines.Single().InstallmentId.Should().Be(installmentId);
    }

    [Fact]
    public async Task Pago_que_excede_el_saldo_pendiente_retorna_ValidationFailure_sin_lanzar()
    {
        var (payments, payables, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);
        payable.RegisterPayment(70m, UserId); // saldo restante: 30
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        var handler = BuildHandler(payments, payables, tenant, company, user);
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, null, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede el saldo pendiente");
        payable.PaidAmount.Should().Be(70m, "el pago rechazado no debe mutar el saldo ya registrado");
        payments.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pago_sobre_CxP_inexistente_retorna_NotFound()
    {
        var (payments, payables, tenant, company, user) = BuildMocks();
        var missingId = Guid.NewGuid();
        payables
            .Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchasePayable?)null);

        var handler = BuildHandler(payments, payables, tenant, company, user);
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(missingId, null, 50m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Pago_sobre_CxP_anulada_retorna_ValidationFailure()
    {
        var (payments, payables, tenant, company, user) = BuildMocks();
        var payable = CreatePayable(100m);
        payable.CancelPayable();
        payables
            .Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        var handler = BuildHandler(payments, payables, tenant, company, user);
        var cmd = new RegisterPaymentCommand(
            SupplierId,
            10m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(payable.Id, null, 10m) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anulada");
    }
}
