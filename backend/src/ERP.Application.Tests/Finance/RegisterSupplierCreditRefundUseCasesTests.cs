using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — RegisterSupplierCreditRefundHandler:
/// reembolso feliz banco/caja, SC-001, SC-020, SC-021, SC-024, SC-015, SC-027, SC-003,
/// idempotencia (SC-006). CompanyBankAccount/CashRegister reemplazan a legacy treasury destination
/// — no hay validación de moneda (ninguno de los dos modelos tiene CurrencyCode propio).
/// </summary>
public sealed class RegisterSupplierCreditRefundUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();

    private static SupplierCredit BuildCredit(decimal amount = 100m, string currency = "USD") =>
        SupplierCredit.CreateFromReturn(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            currency,
            Guid.NewGuid(),
            amount,
            UserId
        );

    private static Account BuildAccount(bool active = true, bool allowsPosting = true)
    {
        var acc = Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create("1.1.01"),
            "Banco Pichincha",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: allowsPosting,
            createdBy: UserId
        );
        if (!active)
            acc.Disable(UserId);
        return acc;
    }

    private static CompanyBankAccount BuildBankAccount(bool active = true) =>
        CompanyBankAccount
            .Create(
                TenantId,
                CompanyId,
                BankId,
                BankAccountType.Checking,
                "1234567890",
                "Banco Pichincha CTE",
                AccountId,
                UserId
            )
            .Also(d =>
            {
                if (!active)
                    d.Disable(UserId);
            });

    private static CashRegister BuildCashRegister()
    {
        var register = CashRegister.Create(TenantId, CompanyId, BranchId, "CASH-01", "Caja Matriz", UserId);
        register.SetAccountingAccount(AccountId, UserId);
        return register;
    }

    private static PaymentMethod BuildPaymentMethod(
        bool active = true,
        bool requiresReference = false
    )
    {
        var pm = PaymentMethod.Create(
            TenantId,
            "TRANSFER",
            "Transferencia",
            requiresReference,
            false,
            1,
            UserId
        );
        if (!active)
            pm.Disable(UserId);
        return pm;
    }

    private static CashSession BuildOpenCashSession() =>
        CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            CashRegisterId,
            "CAJA-01",
            "Caja Matriz",
            Guid.NewGuid(),
            "001-001",
            0m,
            UserId
        );

    private sealed class Mocks
    {
        public Mock<ISupplierCreditRepository> CreditRepo { get; } = new();
        public Mock<ISupplierCreditRefundTransactionRepository> TxRepo { get; } = new();
        public Mock<ICompanyBankAccountRepository> BankAccountRepo { get; } = new();
        public Mock<ICashRegisterRepository> CashRegisterRepo { get; } = new();
        public Mock<IAccountRepository> AccountRepo { get; } = new();
        public Mock<IPaymentMethodRepository> PaymentMethodRepo { get; } = new();
        public Mock<ICashSessionRepository> CashSessionRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(SupplierCredit credit)
        {
            CreditRepo
                .Setup(r => r.GetByIdAsync(TenantId, credit.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(credit);
            Uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        }

        public RegisterSupplierCreditRefundHandler BuildHandler() =>
            new(
                CreditRepo.Object,
                TxRepo.Object,
                BankAccountRepo.Object,
                CashRegisterRepo.Object,
                AccountRepo.Object,
                PaymentMethodRepo.Object,
                CashSessionRepo.Object,
                Uow.Object,
                DbEx.Object,
                new FixedCurrentTenant(),
                new FixedCurrentUser()
            );
    }

    private static RegisterSupplierCreditRefundCommand BankCommand(
        Guid supplierCreditId,
        decimal amount,
        DateOnly? effectiveDate = null,
        string? externalReference = null,
        Guid? clientRequestId = null
    ) =>
        new(
            supplierCreditId,
            BankAccountId,
            null,
            "TRANSFER",
            amount,
            effectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            externalReference,
            clientRequestId ?? Guid.NewGuid()
        );

    private static RegisterSupplierCreditRefundCommand CashCommand(
        Guid supplierCreditId,
        decimal amount,
        DateOnly? effectiveDate = null,
        Guid? clientRequestId = null
    ) =>
        new(
            supplierCreditId,
            null,
            CashRegisterId,
            "TRANSFER",
            amount,
            effectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            clientRequestId ?? Guid.NewGuid()
        );

    [Fact]
    public async Task Reembolso_feliz_banco_reduce_AvailableAmount_y_crea_transaccion()
    {
        var credit = BuildCredit(100m);
        var m = new Mocks(credit);
        var bankAccount = BuildBankAccount();
        var account = BuildAccount();
        var pm = BuildPaymentMethod();
        m.BankAccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, BankAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(bankAccount);
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(
                    TenantId,
                    CompanyId,
                    AccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(account);
        m.PaymentMethodRepo.Setup(r =>
                r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(pm);
        var handler = m.BuildHandler();

        var result = await handler.Handle(BankCommand(credit.Id, 40m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        credit.AvailableAmount.Should().Be(60m);
        result.Value!.Amount.Should().Be(40m);
        result.Value.AccountingAccountId.Should().Be(account.Id);
        result.Value.CashSessionId.Should().BeNull();
    }

    [Fact]
    public async Task Reembolso_feliz_caja_con_sesion_activa_vincula_CashMovement()
    {
        var credit = BuildCredit(100m);
        var m = new Mocks(credit);
        var destination = BuildCashRegister();
        var account = BuildAccount();
        var pm = BuildPaymentMethod();
        var session = BuildOpenCashSession();
        m.CashRegisterRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(destination);
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(
                    TenantId,
                    CompanyId,
                    AccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(account);
        m.PaymentMethodRepo.Setup(r =>
                r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(pm);
        m.CashSessionRepo.Setup(r =>
                r.GetOpenByCashRegisterForUpdateAsync(
                    TenantId,
                    CashRegisterId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(session);
        var handler = m.BuildHandler();

        var result = await handler.Handle(CashCommand(credit.Id, 40m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CashSessionId.Should().Be(session.Id);
        result.Value.CashMovementId.Should().NotBeNull();
        session.Movements.Should().HaveCount(2, "apertura + el movimiento del reembolso");
    }

    /// <summary>
    /// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — el proveedor DEVUELVE efectivo: el dinero
    /// entra al cajón, así que el saldo esperado de la sesión debe AUMENTAR por el monto del
    /// reembolso (antes se registraba como egreso y el esperado bajaba).
    /// </summary>
    [Fact]
    public async Task Reembolso_en_efectivo_es_ingreso_y_aumenta_el_saldo_esperado_de_caja()
    {
        var credit = BuildCredit(100m);
        var m = new Mocks(credit);
        var session = BuildOpenCashSession();
        m.CashRegisterRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildCashRegister());
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo.Setup(r =>
                r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildPaymentMethod());
        m.CashSessionRepo.Setup(r =>
                r.GetOpenByCashRegisterForUpdateAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(session);
        var balanceBefore = session.CurrentBalance;

        var result = await m.BuildHandler().Handle(CashCommand(credit.Id, 40m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var movement = session.Movements.Single(x => x.Id == result.Value!.CashMovementId);
        movement.MovementType.Should().Be(ERP.Domain.Modules.Caja.Enums.CashMovementType.ManualIncome);
        session.CurrentBalance.Should().Be(balanceBefore + 40m);
    }

    [Fact]
    public async Task Credito_inexistente_rechaza_SC_001()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        var missingId = Guid.NewGuid();
        m.CreditRepo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierCredit?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(BankCommand(missingId, 10m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Destino_inexistente_rechaza_SC_020()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.BankAccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, BankAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyBankAccount?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(BankCommand(credit.Id, 10m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        credit.AvailableAmount.Should().Be(100m);
    }

    [Fact]
    public async Task Destino_inactivo_rechaza_SC_021()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.BankAccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, BankAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildBankAccount(active: false));
        var handler = m.BuildHandler();

        var result = await handler.Handle(BankCommand(credit.Id, 10m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("activa");
    }

    [Fact]
    public async Task Cuenta_no_postable_rechaza_SC_024()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.BankAccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, BankAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildBankAccount());
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(
                    TenantId,
                    CompanyId,
                    AccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BuildAccount(allowsPosting: false));
        var handler = m.BuildHandler();

        var result = await handler.Handle(BankCommand(credit.Id, 10m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("contabilización");
    }

    [Fact]
    public async Task Metodo_de_pago_inactivo_rechaza_SC_015()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.BankAccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, BankAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildBankAccount());
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(
                    TenantId,
                    CompanyId,
                    AccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo.Setup(r =>
                r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildPaymentMethod(active: false));
        var handler = m.BuildHandler();

        var result = await handler.Handle(BankCommand(credit.Id, 10m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("método de pago");
    }

    [Fact]
    public async Task Caja_sin_sesion_activa_rechaza_SC_027()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.CashRegisterRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildCashRegister());
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(
                    TenantId,
                    CompanyId,
                    AccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo.Setup(r =>
                r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildPaymentMethod());
        m.CashSessionRepo.Setup(r =>
                r.GetOpenByCashRegisterForUpdateAsync(
                    TenantId,
                    CashRegisterId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((CashSession?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(CashCommand(credit.Id, 10m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sesión de caja");
        credit
            .AvailableAmount.Should()
            .Be(100m, "ni siquiera el movimiento de crédito debe persistir");
    }

    [Fact]
    public async Task Sobreaplicacion_rechaza_SC_003()
    {
        var credit = BuildCredit(30m);
        var m = new Mocks(credit);
        m.BankAccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, BankAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildBankAccount());
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(
                    TenantId,
                    CompanyId,
                    AccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo.Setup(r =>
                r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildPaymentMethod());
        var handler = m.BuildHandler();

        var result = await handler.Handle(BankCommand(credit.Id, 50m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        credit.AvailableAmount.Should().Be(30m);
    }

    [Fact]
    public async Task Reintento_con_mismo_ClientRequestId_y_mismo_payload_no_duplica()
    {
        var credit = BuildCredit(100m);
        var m = new Mocks(credit);
        m.BankAccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, BankAccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildBankAccount());
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(
                    TenantId,
                    CompanyId,
                    AccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo.Setup(r =>
                r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildPaymentMethod());
        var handler = m.BuildHandler();
        var cri = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var first = await handler.Handle(
            BankCommand(credit.Id, 40m, date, clientRequestId: cri),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue(first.Error);

        var firstMovement = credit.Movements.Single();
        m.TxRepo.Setup(r =>
                r.GetBySupplierCreditMovementIdAsync(
                    TenantId,
                    firstMovement.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                SupplierCreditRefundTransaction.CreateReceived(
                    TenantId,
                    CompanyId,
                    SupplierId,
                    credit.Id,
                    firstMovement.Id,
                    BankAccountId,
                    null,
                    AccountId,
                    "1.1.01",
                    "1234567890",
                    "Banco Pichincha CTE",
                    "BankAccount",
                    "TRANSFER",
                    40m,
                    "USD",
                    date,
                    UserId,
                    cri,
                    firstMovement.RequestPayloadHash
                )
            );

        var retry = await handler.Handle(
            BankCommand(credit.Id, 40m, date, clientRequestId: cri),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue(retry.Error);
        credit
            .Movements.Should()
            .HaveCount(1, "no debe duplicar el movimiento en un reintento idempotente");
        credit.AvailableAmount.Should().Be(60m);
    }

    private sealed class FixedCurrentTenant : ICurrentTenant
    {
        public Guid TenantId => RegisterSupplierCreditRefundUseCasesTests.TenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public Guid UserId => RegisterSupplierCreditRefundUseCasesTests.UserId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-HARDENING-02A-CLOSE — trazabilidad refund → CashMovement
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reembolso_en_efectivo_genera_CashMovement_que_referencia_a_la_transaccion_de_reembolso()
    {
        var credit = BuildCredit(100m);
        var m = new Mocks(credit);
        var session = BuildOpenCashSession();
        m.CashRegisterRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildCashRegister());
        m.AccountRepo.Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo.Setup(r =>
                r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildPaymentMethod());
        m.CashSessionRepo.Setup(r =>
                r.GetOpenByCashRegisterForUpdateAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(session);
        var command = new RegisterSupplierCreditRefundCommand(
            credit.Id,
            null,
            CashRegisterId,
            "TRANSFER",
            40m,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "REC-0042",
            Guid.NewGuid()
        );

        var result = await m.BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var movement = session.Movements.Single(x => x.Id == result.Value!.CashMovementId);
        movement.ReferenceType.Should().Be(ERP.Domain.Modules.Caja.Enums.CashReferenceType.SupplierCreditRefund);
        movement.ReferenceId.Should().Be(result.Value!.Id, "el movimiento apunta a la transacción de reembolso");
        movement.ReferenceNumber.Should().Be("REC-0042");
        result.Value.CashSessionId.Should().Be(session.Id);
    }
}

file static class TestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
