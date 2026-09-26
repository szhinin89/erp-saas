using ERP.Application.Common;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Payables;

/// <summary>
/// SUPPLIER-PAYMENTS-REVERSE-16 — cobertura de orquestación de
/// <see cref="ReverseSupplierPaymentCommandHandler"/>: transición de estado, reversa de saldos por
/// cuota (nunca por FIFO) y manejo de fallos (concurrencia, posting) dentro de la transacción
/// explícita. Las reglas de dominio puras (bloquear doble reversa, motivo obligatorio) ya están
/// cubiertas en <c>SupplierPaymentTests</c>/<c>AccountsPayableTests</c> (Domain).
/// </summary>
public sealed class ReverseSupplierPaymentUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed record Mocks(
        Mock<ISupplierPaymentRepository> SupplierPayments,
        Mock<IAccountsPayableRepository> AccountsPayables,
        Mock<ICashSessionRepository> CashSessions,
        Mock<IUnitOfWork> Uow,
        Mock<ICurrentTenant> Tenant,
        Mock<ICurrentCompany> Company,
        Mock<ICurrentUser> User
    );

    private static Mocks BuildMocks()
    {
        var supplierPayments = new Mock<ISupplierPaymentRepository>();
        var accountsPayables = new Mock<IAccountsPayableRepository>();
        var cashSessions = new Mock<ICashSessionRepository>();
        var uow = new Mock<IUnitOfWork>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var user = new Mock<ICurrentUser>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        user.Setup(u => u.UserId).Returns(UserId);

        return new Mocks(supplierPayments, accountsPayables, cashSessions, uow, tenant, company, user);
    }

    private static ReverseSupplierPaymentCommandHandler BuildHandler(Mocks m) =>
        new(
            m.SupplierPayments.Object,
            m.AccountsPayables.Object,
            m.CashSessions.Object,
            m.Uow.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.User.Object
        );

    private static AccountsPayable CreatePayableWithInstallment(decimal amount, out Guid installmentId)
    {
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            UserId
        );
        var installment = payable.AddInstallment(1, new DateOnly(2026, 9, 1), amount);
        installmentId = installment.Id;
        return payable;
    }

    private static SupplierPayment CreateConfirmedPayment(
        AccountsPayable payable,
        Guid installmentId,
        decimal amount
    )
    {
        var methods = new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, amount, TransactionDate: new DateOnly(2026, 8, 28)) };
        var applications = new[] { new SupplierPaymentApplicationLineInput(installmentId, amount) };
        var allocations = new[] { new SupplierPaymentAllocationInput(0, 0, amount) };
        var payment = SupplierPayment.Create(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            new DateOnly(2026, 8, 28),
            amount,
            "00000001",
            null,
            methods,
            applications,
            allocations,
            UserId
        );
        payable.RegisterPaymentToInstallment(installmentId, amount, UserId);
        return payment;
    }

    private void SetupPayment(Mocks m, SupplierPayment payment) =>
        m.SupplierPayments
            .Setup(r => r.GetByIdAsync(TenantId, payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

    private void SetupPayable(Mocks m, AccountsPayable payable, Guid installmentId) =>
        m.AccountsPayables
            .Setup(a => a.GetByInstallmentIdAsync(TenantId, installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

    [Fact]
    public async Task Reversar_pago_Confirmed_cambia_estado_a_Reversed()
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var handler = BuildHandler(m);
        var result = await handler.Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Error de digitación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.Status.Should().Be("Reversed");
        payment.Status.Should().Be(SupplierPaymentStatus.Reversed);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reversar_pago_total_vuelve_la_cuota_de_Paid_a_Pending()
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        payable.Installments[0].Status.Should().Be(AccountsPayableStatus.Paid);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var handler = BuildHandler(m);
        var result = await handler.Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(because: result.Error);
        payable.Installments[0].Status.Should().Be(AccountsPayableStatus.Pending);
        payable.Installments[0].PaidAmount.Should().Be(0m);
        payable.Installments[0].OutstandingAmount.Should().Be(300m);
        payable.Status.Should().Be(AccountsPayableStatus.Pending);
    }

    [Fact]
    public async Task Reversar_pago_parcial_deja_saldos_correctos()
    {
        var m = BuildMocks();
        // Cuota de 300, se pagan 100 (parcial) — se registra y reversa ese mismo pago de 100.
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 100m);
        payable.Installments[0].Status.Should().Be(AccountsPayableStatus.PartiallyPaid);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var handler = BuildHandler(m);
        var result = await handler.Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Cheque rechazado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(because: result.Error);
        payable.Installments[0].PaidAmount.Should().Be(0m);
        payable.Installments[0].OutstandingAmount.Should().Be(300m);
        payable.Installments[0].Status.Should().Be(AccountsPayableStatus.Pending);
    }

    [Fact]
    public async Task Bloquea_doble_reversa()
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        payment.Reverse("Primera reversa", UserId, DateTime.UtcNow);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var handler = BuildHandler(m);
        var result = await handler.Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Segundo intento"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bloquea_reversa_sin_motivo()
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var handler = BuildHandler(m);
        var result = await handler.Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "   "),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pago_inexistente_retorna_NotFound()
    {
        var m = BuildMocks();
        var missingId = Guid.NewGuid();
        m.SupplierPayments
            .Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierPayment?)null);

        var handler = BuildHandler(m);
        var result = await handler.Handle(
            new ReverseSupplierPaymentCommand(missingId, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Fallo_de_posting_inverso_hace_rollback_completo_y_el_pago_sigue_Confirmed()
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.SupplierPayments
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new SupplierPaymentPostingFailedException(
                    "No existe regla de contabilización.",
                    "RULE_NOT_FOUND"
                )
            );

        var handler = BuildHandler(m);
        var result = await handler.Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Error de digitación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("RULE_NOT_FOUND");
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        // El objeto en memoria refleja el intento (Reverse() ya corrió antes del SaveChanges
        // fallido), pero como nada se persistió, la transacción de BD se revierte por completo —
        // el rollback real ocurre a nivel de base de datos (ver ERP.Infrastructure.Tests E2E).
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A
    // ══════════════════════════════════════════════════════════════════════

    private static CashSession OpenSession(Guid cashRegisterId, decimal openingAmount) =>
        CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            cashRegisterId,
            "CAJA-01",
            "Caja Principal",
            Guid.NewGuid(),
            "001",
            openingAmount,
            UserId
        );

    /// <summary>Pago en efectivo confirmado tal como lo deja RegisterSupplierPayment en 02A: egreso
    /// SupplierPayment registrado en la sesión y vinculado a la línea.</summary>
    private static SupplierPayment CreateConfirmedCashPayment(
        AccountsPayable payable,
        Guid installmentId,
        decimal amount,
        CashSession session
    )
    {
        var payment = SupplierPayment.Create(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            new DateOnly(2026, 8, 28),
            amount,
            "00000001",
            null,
            new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), null, session.CashRegisterId, amount) },
            new[] { new SupplierPaymentApplicationLineInput(installmentId, amount) },
            new[] { new SupplierPaymentAllocationInput(0, 0, amount) },
            UserId
        );
        var movement = session.RecordMovement(
            CashMovementType.SupplierPayment,
            amount,
            "Pago a proveedor 00000001",
            UserId,
            CashReferenceType.SupplierPayment,
            payment.Id,
            "00000001"
        );
        payment.LinkCashMovement(payment.MethodLines[0].Id, session.Id, movement.Id);
        payable.RegisterPaymentToInstallment(installmentId, amount, UserId);
        return payment;
    }

    [Fact]
    public async Task Reversa_de_pago_en_efectivo_registra_ingreso_compensatorio_sin_borrar_el_egreso_original()
    {
        var m = BuildMocks();
        var session = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(120m, out var installmentId);
        var payment = CreateConfirmedCashPayment(payable, installmentId, 120m, session);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r =>
                r.GetOpenByCashRegisterForUpdateAsync(TenantId, session.CashRegisterId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(session);
        session.CurrentBalance.Should().Be(380m, "precondición: el pago ya sacó 120 del cajón");

        var result = await BuildHandler(m).Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Pago duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        session.Movements.Should().HaveCount(3, "apertura + egreso original intacto + ingreso compensatorio");
        var original = session.Movements.Single(x => x.Id == payment.MethodLines[0].CashMovementId);
        original.MovementType.Should().Be(CashMovementType.SupplierPayment);
        var compensation = session.Movements.Single(x => x.MovementType == CashMovementType.SupplierPaymentReversal);
        compensation.Amount.Should().Be(120m);
        compensation.ReferenceType.Should().Be(CashReferenceType.SupplierPayment);
        compensation.ReferenceId.Should().Be(payment.Id);
        session.CurrentBalance.Should().Be(500m, "el saldo esperado vuelve al estado previo al pago");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reversa_de_pago_en_efectivo_sin_sesion_abierta_se_rechaza_y_el_pago_sigue_Confirmed()
    {
        var m = BuildMocks();
        var session = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(120m, out var installmentId);
        var payment = CreateConfirmedCashPayment(payable, installmentId, 120m, session);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r =>
                r.GetOpenByCashRegisterForUpdateAsync(TenantId, session.CashRegisterId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CashSession?)null);

        var result = await BuildHandler(m).Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Pago duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sesión de caja abierta");
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reversa_de_pago_bancario_no_toca_caja()
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var result = await BuildHandler(m).Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Error"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        m.CashSessions.VerifyNoOtherCalls();
    }
}
