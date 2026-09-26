using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
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
        Mock<ISupplierCreditRepository> SupplierCredits,
        Mock<IUnitOfWork> Uow,
        Mock<ICurrentTenant> Tenant,
        Mock<ICurrentCompany> Company,
        Mock<ICurrentUser> User,
        Mock<ICurrentBranch> Branch,
        Mock<IBranchAccessGuard> BranchAccess
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
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(BranchId);
        branch.Setup(b => b.HasBranchContext).Returns(true);
        var branchAccess = new Mock<IBranchAccessGuard>();
        branchAccess
            .Setup(g => g.RequireBranchAsync(BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Success(
                    new BranchAccessContext(UserId, TenantId, CompanyId, BranchId, "Matriz", true)
                )
            );

        return new Mocks(
            supplierPayments,
            accountsPayables,
            cashSessions,
            new Mock<ISupplierCreditRepository>(),
            uow,
            tenant,
            company,
            user,
            branch,
            branchAccess
        );
    }

    private static ReverseSupplierPaymentCommandHandler BuildHandler(Mocks m) =>
        new(
            m.SupplierPayments.Object,
            m.AccountsPayables.Object,
            m.CashSessions.Object,
            m.SupplierCredits.Object,
            m.Uow.Object,
            m.Tenant.Object,
            m.Company.Object,
            m.Branch.Object,
            m.BranchAccess.Object,
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
            ValidReversal(payment.Id, "Error de digitación"),
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
            ValidReversal(payment.Id, "Duplicado"),
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
            ValidReversal(payment.Id, "Cheque rechazado"),
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
        payment.Reverse("Primera reversa", UserId, DateTime.UtcNow, bankReversalReason: SupplierPaymentBankReversalReason.NotExecuted);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var handler = BuildHandler(m);
        var result = await handler.Handle(
            ValidReversal(payment.Id, "Segundo intento"),
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
            ValidReversal(payment.Id, "   "),
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
            ValidReversal(missingId, "Motivo"),
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
            ValidReversal(payment.Id, "Error de digitación"),
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
                r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(session);
        session.CurrentBalance.Should().Be(380m, "precondición: el pago ya sacó 120 del cajón");

        var result = await BuildHandler(m).Handle(
            ValidReversal(payment.Id, "Pago duplicado"),
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
    public async Task Reversa_de_pago_en_efectivo_con_sesion_original_cerrada_se_rechaza_y_el_pago_sigue_Confirmed()
    {
        var m = BuildMocks();
        var session = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(120m, out var installmentId);
        var payment = CreateConfirmedCashPayment(payable, installmentId, 120m, session);
        session.Close(UserId, new List<CashClosingCount>(), "cierre de turno");
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var movementsBefore = session.Movements.Count;

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Pago duplicado"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(
            "El efectivo salió de una sesión que ya está cerrada. Si el proveedor devolvió el dinero, registre una devolución de fondos."
        );
        session.Movements.Should().HaveCount(movementsBefore);
        payable.Installments[0].PaidAmount.Should().Be(120m, "la CxP no se altera");
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
            ValidReversal(payment.Id, "Error"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        m.CashSessions.VerifyNoOtherCalls();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-OWNERSHIP-02B
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reversa_de_pago_en_efectivo_sobre_caja_operada_por_otro_usuario_se_rechaza()
    {
        var m = BuildMocks();
        var foreignSession = CashSession.Open(
            TenantId, CompanyId, BranchId, Guid.NewGuid(), Guid.NewGuid(),
            "CAJA-01", "Caja Principal", Guid.NewGuid(), "001", 500m, Guid.NewGuid()
        );
        var payable = CreatePayableWithInstallment(120m, out var installmentId);
        var payment = CreateConfirmedCashPayment(payable, installmentId, 120m, foreignSession);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, foreignSession.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignSession);
        var movementsBefore = foreignSession.Movements.Count;

        var result = await BuildHandler(m).Handle(
            ValidReversal(payment.Id, "Pago duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La caja seleccionada está siendo operada por otro usuario.");
        foreignSession.Movements.Should().HaveCount(movementsBefore, "no se compensa silenciosamente en la caja ajena");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-OWNERSHIP-02B-CLOSE — sucursal activa en reversas con efecto en caja
    // ══════════════════════════════════════════════════════════════════════

    private (Mocks M, SupplierPayment Payment, CashSession Session) CashReversalScenario(Guid sessionBranchId)
    {
        var m = BuildMocks();
        var session = CashSession.Open(
            TenantId, CompanyId, sessionBranchId, UserId, Guid.NewGuid(),
            "CAJA-01", "Caja Principal", Guid.NewGuid(), "001", 500m, UserId
        );
        var payable = CreatePayableWithInstallment(120m, out var installmentId);
        var payment = CreateConfirmedCashPayment(payable, installmentId, 120m, session);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        return (m, payment, session);
    }

    [Fact]
    public async Task Reversa_con_efecto_en_caja_de_otra_sucursal_se_rechaza()
    {
        var (m, payment, session) = CashReversalScenario(sessionBranchId: Guid.NewGuid());
        var movementsBefore = session.Movements.Count;

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Error"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La caja seleccionada no pertenece a la sucursal activa.");
        session.Movements.Should().HaveCount(movementsBefore);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reversa_con_efecto_en_caja_sin_sucursal_activa_se_rechaza()
    {
        var (m, payment, session) = CashReversalScenario(sessionBranchId: BranchId);
        m.Branch.Setup(b => b.BranchId).Returns(Guid.Empty);
        m.Branch.Setup(b => b.HasBranchContext).Returns(false);

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Error"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("seleccione la sucursal activa");
        m.CashSessions.Verify(
            r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reversa_con_efecto_en_caja_y_sucursal_sin_acceso_se_rechaza_via_guard_oficial()
    {
        var (m, payment, _) = CashReversalScenario(sessionBranchId: BranchId);
        m.BranchAccess
            .Setup(g => g.RequireBranchAsync(BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Forbidden("No tiene acceso a esta sucursal."));

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Error"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta sucursal.");
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reversa_bancaria_no_exige_sucursal_activa()
    {
        var m = BuildMocks();
        m.Branch.Setup(b => b.BranchId).Returns(Guid.Empty);
        m.Branch.Setup(b => b.HasBranchContext).Returns(false);
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Error"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        m.BranchAccess.VerifyNoOtherCalls();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL — reversa = corrección documental
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Reversa documental válida: confirma que el efectivo no se entregó y declara que la transferencia no se ejecutó.</summary>
    private static ReverseSupplierPaymentCommand ValidReversal(Guid paymentId, string reason) =>
        new(paymentId, reason, CashNotDeliveredConfirmed: true, BankReversalReason: SupplierPaymentBankReversalReason.NotExecuted);

    private static SupplierPayment CreateConfirmedMixedPayment(
        AccountsPayable payable,
        Guid installmentId,
        decimal cashAmount,
        decimal bankAmount,
        CashSession session
    )
    {
        var total = cashAmount + bankAmount;
        var payment = SupplierPayment.Create(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            new DateOnly(2026, 8, 28),
            total,
            "00000001",
            null,
            new[]
            {
                new SupplierPaymentMethodLineInput(Guid.NewGuid(), null, session.CashRegisterId, cashAmount),
                new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, bankAmount, "OP-1", TransactionDate: new DateOnly(2026, 8, 28)),
            },
            new[] { new SupplierPaymentApplicationLineInput(installmentId, total) },
            new[] { new SupplierPaymentAllocationInput(0, 0, cashAmount), new SupplierPaymentAllocationInput(1, 0, bankAmount) },
            UserId
        );
        var movement = session.RecordMovement(
            CashMovementType.SupplierPayment, cashAmount, "Pago a proveedor 00000001", UserId,
            CashReferenceType.SupplierPayment, payment.Id, "00000001"
        );
        payment.LinkCashMovement(payment.MethodLines[0].Id, session.Id, movement.Id);
        payable.RegisterPaymentToInstallment(installmentId, total, UserId);
        return payment;
    }

    [Fact]
    public async Task Efectivo_con_sesion_original_abierta_y_confirmacion_revierte_en_esa_misma_sesion()
    {
        var m = BuildMocks();
        var session = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(120m, out var installmentId);
        var payment = CreateConfirmedCashPayment(payable, installmentId, 120m, session);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await BuildHandler(m).Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Registrado por error", CashNotDeliveredConfirmed: true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ReversalCashNotDeliveredConfirmed.Should().BeTrue();
        result.Value.ReversalBankReason.Should().BeNull("el pago no tiene fuentes bancarias");
        session.Movements.Should().ContainSingle(x => x.MovementType == CashMovementType.SupplierPaymentReversal);
        session.CurrentBalance.Should().Be(500m);
    }

    [Fact]
    public async Task Otra_sesion_abierta_de_la_misma_caja_no_sirve_para_revertir()
    {
        var m = BuildMocks();
        var originalSession = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(120m, out var installmentId);
        var payment = CreateConfirmedCashPayment(payable, installmentId, 120m, originalSession);
        originalSession.Close(UserId, new List<CashClosingCount>(), "cierre de turno");
        // Turno nuevo, abierto por el MISMO usuario en la MISMA caja: no es la sesión de la que salió el efectivo.
        var newShift = OpenSession(originalSession.CashRegisterId, 300m);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, originalSession.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalSession);
        m.CashSessions
            .Setup(r => r.GetOpenByCashRegisterForUpdateAsync(TenantId, originalSession.CashRegisterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newShift);

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Pago duplicado"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("El efectivo salió de una sesión que ya está cerrada.");
        newShift.Movements.Should().ContainSingle("el turno nuevo nunca recibe la compensación");
        m.CashSessions.Verify(
            r => r.GetOpenByCashRegisterForUpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Efectivo_sin_confirmacion_explicita_se_rechaza_sin_alterar_nada()
    {
        var m = BuildMocks();
        var session = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(120m, out var installmentId);
        var payment = CreateConfirmedCashPayment(payable, installmentId, 120m, session);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await BuildHandler(m).Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Pago duplicado", CashNotDeliveredConfirmed: false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Debe confirmar que el efectivo no fue entregado al proveedor y permanece en la misma caja.");
        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payable.Installments[0].PaidAmount.Should().Be(120m);
        session.Movements.Should().HaveCount(2, "apertura + egreso original, sin compensación");
        m.SupplierPayments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SupplierPaymentBankReversalReason.NotExecuted)]
    [InlineData(SupplierPaymentBankReversalReason.RejectedByBank)]
    [InlineData(SupplierPaymentBankReversalReason.RegistrationError)]
    public async Task Banco_con_motivo_estructurado_revierte_y_lo_registra(SupplierPaymentBankReversalReason reason)
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var result = await BuildHandler(m).Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Transferencia no ejecutada", BankReversalReason: reason),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        payment.ReversalBankReason.Should().Be(reason);
        result.Value!.ReversalBankReason.Should().Be(reason.ToString());
        result.Value.ReversalCashNotDeliveredConfirmed.Should().BeNull("el pago no tiene fuentes de caja");
    }

    [Fact]
    public async Task Banco_sin_motivo_estructurado_se_rechaza_sin_alterar_nada()
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

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("Debe indicar el motivo de la reversa bancaria");
        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payable.Installments[0].PaidAmount.Should().Be(300m);
        m.SupplierPayments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pago_mixto_donde_todas_las_fuentes_califican_se_revierte_completo()
    {
        var m = BuildMocks();
        var session = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(200m, out var installmentId);
        var payment = CreateConfirmedMixedPayment(payable, installmentId, 80m, 120m, session);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await BuildHandler(m).Handle(
            new ReverseSupplierPaymentCommand(
                payment.Id,
                "Pago duplicado",
                CashNotDeliveredConfirmed: true,
                BankReversalReason: SupplierPaymentBankReversalReason.RejectedByBank
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        payment.Status.Should().Be(SupplierPaymentStatus.Reversed);
        payable.Installments[0].PaidAmount.Should().Be(0m);
        session.CurrentBalance.Should().Be(500m, "solo la fuente de caja se compensa, en su sesión original");
        payment.ReversalBankReason.Should().Be(SupplierPaymentBankReversalReason.RejectedByBank);
        payment.ReversalCashNotDeliveredConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task Pago_mixto_donde_la_fuente_de_caja_no_califica_rechaza_la_reversa_completa()
    {
        var m = BuildMocks();
        var session = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(200m, out var installmentId);
        var payment = CreateConfirmedMixedPayment(payable, installmentId, 80m, 120m, session);
        session.Close(UserId, new List<CashClosingCount>(), "cierre de turno");
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var movementsBefore = session.Movements.Count;

        // La fuente bancaria sí califica (NotExecuted), pero la de caja salió de una sesión cerrada.
        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Pago duplicado"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("El efectivo salió de una sesión que ya está cerrada.");
        payable.Installments[0].PaidAmount.Should().Be(200m, "sin reversa parcial: la CxP queda intacta");
        session.Movements.Should().HaveCount(movementsBefore);
        m.SupplierPayments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pago_mixto_sin_motivo_bancario_rechaza_la_reversa_completa_aunque_la_caja_califique()
    {
        var m = BuildMocks();
        var session = OpenSession(Guid.NewGuid(), 500m);
        var payable = CreatePayableWithInstallment(200m, out var installmentId);
        var payment = CreateConfirmedMixedPayment(payable, installmentId, 80m, 120m, session);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        m.CashSessions
            .Setup(r => r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await BuildHandler(m).Handle(
            new ReverseSupplierPaymentCommand(payment.Id, "Pago duplicado", CashNotDeliveredConfirmed: true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("Debe indicar el motivo de la reversa bancaria");
        session.Movements.Should().HaveCount(2, "apertura + egreso original, sin compensación");
        payable.Installments[0].PaidAmount.Should().Be(200m);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — reversa de un pago que generó anticipo
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Transferencia de 200 aplicando 180 a la cuota (anticipo 20) + su SupplierCredit.</summary>
    private static (SupplierPayment payment, SupplierCredit credit) CreateOverpayment(AccountsPayable payable, Guid installmentId)
    {
        var payment = SupplierPayment.Create(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            new DateOnly(2026, 8, 28),
            200m,
            "00000002",
            null,
            new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), null, 200m, TransactionDate: new DateOnly(2026, 8, 28)) },
            new[] { new SupplierPaymentApplicationLineInput(installmentId, 180m) },
            new[] { new SupplierPaymentAllocationInput(0, 0, 180m) },
            UserId,
            unappliedAmountConfirmed: true
        );
        payable.RegisterPaymentToInstallment(installmentId, 180m, UserId);
        var credit = SupplierCredit.CreateFromSupplierPayment(
            TenantId, CompanyId, BranchId, SupplierId, "USD", payment.Id, payment.UnappliedAmount, UserId
        );
        return (payment, credit);
    }

    private static void SetupCredit(Mocks m, SupplierPayment payment, SupplierCredit credit)
    {
        m.SupplierCredits
            .Setup(r => r.GetIdBySourceSupplierPaymentIdAsync(TenantId, payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credit.Id);
        m.SupplierCredits
            .Setup(r => r.GetByIdAsync(TenantId, credit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credit);
    }

    [Fact]
    public async Task Reversa_con_anticipo_intacto_bloquea_el_credito_lo_anula_y_revierte_la_CxP()
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(180m, out var installmentId);
        var (payment, credit) = CreateOverpayment(payable, installmentId);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        SetupCredit(m, payment, credit);

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "No ejecutada"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.SupplierCreditId.Should().Be(credit.Id);
        payment.Status.Should().Be(SupplierPaymentStatus.Reversed);
        credit.AvailableAmount.Should().Be(0m);
        credit.OriginalAmount.Should().Be(20m);
        credit.Movements.Should().ContainSingle(mv => mv.MovementType == SupplierCreditMovementType.SourcePaymentReversed);
        payable.Installments[0].PaidAmount.Should().Be(0m);
        m.SupplierCredits.Verify(r => r.AcquireLockAsync(TenantId, credit.Id, It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reversa_con_anticipo_aplicado_o_reembolsado_se_rechaza_sin_tocar_pago_CxP_ni_credito(bool applied)
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(180m, out var installmentId);
        var (payment, credit) = CreateOverpayment(payable, installmentId);
        if (applied)
            credit.ApplyToPayable(Guid.NewGuid(), 5m, UserId, Guid.NewGuid(), "h");
        else
            credit.RegisterRefund(20m, UserId, Guid.NewGuid(), "h");
        var availableBefore = credit.AvailableAmount;
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);
        SetupCredit(m, payment, credit);

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Intento"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anticipo que generó ya fue aplicado o reembolsado");
        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payable.Installments[0].PaidAmount.Should().Be(180m);
        credit.AvailableAmount.Should().Be(availableBefore);
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reversa_de_pago_sin_remanente_no_consulta_SupplierCredit()
    {
        var m = BuildMocks();
        var payable = CreatePayableWithInstallment(300m, out var installmentId);
        var payment = CreateConfirmedPayment(payable, installmentId, 300m);
        SetupPayment(m, payment);
        SetupPayable(m, payable, installmentId);

        var result = await BuildHandler(m).Handle(ValidReversal(payment.Id, "Duplicado"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        m.SupplierCredits.Verify(
            r => r.GetIdBySourceSupplierPaymentIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
