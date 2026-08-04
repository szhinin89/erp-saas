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
/// P0-02 Fase 8 — RegisterSupplierCreditRefundHandler: reembolso feliz banco/caja, SC-001, SC-020,
/// SC-021, SC-024, SC-025, SC-015, SC-027, SC-003, idempotencia (SC-006).
/// </summary>
public sealed class RegisterSupplierCreditRefundUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid DestinationId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();

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

    private static CompanyFinancialDestination BuildBankDestination(
        bool active = true,
        string currency = "USD"
    ) =>
        CompanyFinancialDestination.Create(
            TenantId,
            CompanyId,
            "BANK-01",
            "Banco Pichincha CTE",
            FinancialDestinationTypeCode.BankAccount,
            AccountId,
            currency,
            UserId,
            bankInstitutionCode: "PICHINCHA",
            bankAccountIdentifierNormalized: "1234567890"
        ).Also(d =>
        {
            if (!active)
                d.SetActive(false, UserId);
        });

    private static CompanyFinancialDestination BuildCashDestination(string currency = "USD") =>
        CompanyFinancialDestination.Create(
            TenantId,
            CompanyId,
            "CASH-01",
            "Caja Matriz",
            FinancialDestinationTypeCode.CashRegister,
            AccountId,
            currency,
            UserId,
            cashRegisterId: CashRegisterId
        );

    private static PaymentMethod BuildPaymentMethod(bool active = true, bool requiresReference = false)
    {
        var pm = PaymentMethod.Create(TenantId, "TRANSFER", "Transferencia", requiresReference, false, 1, UserId);
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
        public Mock<ICompanyFinancialDestinationRepository> DestinationRepo { get; } = new();
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
                DestinationRepo.Object,
                AccountRepo.Object,
                PaymentMethodRepo.Object,
                CashSessionRepo.Object,
                Uow.Object,
                DbEx.Object,
                new FixedCurrentTenant(),
                new FixedCurrentUser()
            );
    }

    [Fact]
    public async Task Reembolso_feliz_banco_reduce_AvailableAmount_y_crea_transaccion()
    {
        var credit = BuildCredit(100m);
        var m = new Mocks(credit);
        var destination = BuildBankDestination();
        var account = BuildAccount();
        var pm = BuildPaymentMethod();
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        m.AccountRepo
            .Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(account);
        m.PaymentMethodRepo
            .Setup(r => r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                40m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

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
        var destination = BuildCashDestination();
        var account = BuildAccount();
        var pm = BuildPaymentMethod();
        var session = BuildOpenCashSession();
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(destination);
        m.AccountRepo
            .Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(account);
        m.PaymentMethodRepo
            .Setup(r => r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);
        m.CashSessionRepo
            .Setup(r =>
                r.GetOpenByCashRegisterForShareAsync(
                    TenantId,
                    CashRegisterId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(session);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                40m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CashSessionId.Should().Be(session.Id);
        result.Value.CashMovementId.Should().NotBeNull();
        session.Movements.Should().HaveCount(2, "apertura + el movimiento del reembolso");
    }

    [Fact]
    public async Task Credito_inexistente_rechaza_SC_001()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        var missingId = Guid.NewGuid();
        m.CreditRepo
            .Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierCredit?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                missingId,
                DestinationId,
                "TRANSFER",
                10m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Destino_inexistente_rechaza_SC_020()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyFinancialDestination?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                10m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        credit.AvailableAmount.Should().Be(100m);
    }

    [Fact]
    public async Task Destino_inactivo_rechaza_SC_021()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBankDestination(active: false));
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                10m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("activo");
    }

    [Fact]
    public async Task Moneda_distinta_rechaza_SC_025()
    {
        var credit = BuildCredit(currency: "USD");
        var m = new Mocks(credit);
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBankDestination(currency: "EUR"));
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                10m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("moneda");
    }

    [Fact]
    public async Task Cuenta_no_postable_rechaza_SC_024()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBankDestination());
        m.AccountRepo
            .Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildAccount(allowsPosting: false));
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                10m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("contabilización");
    }

    [Fact]
    public async Task Metodo_de_pago_inactivo_rechaza_SC_015()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBankDestination());
        m.AccountRepo
            .Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo
            .Setup(r => r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPaymentMethod(active: false));
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                10m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("método de pago");
    }

    [Fact]
    public async Task Caja_sin_sesion_activa_rechaza_SC_027()
    {
        var credit = BuildCredit();
        var m = new Mocks(credit);
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCashDestination());
        m.AccountRepo
            .Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo
            .Setup(r => r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPaymentMethod());
        m.CashSessionRepo
            .Setup(r =>
                r.GetOpenByCashRegisterForShareAsync(
                    TenantId,
                    CashRegisterId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((CashSession?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                10m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sesión de caja");
        credit.AvailableAmount.Should().Be(100m, "ni siquiera el movimiento de crédito debe persistir");
    }

    [Fact]
    public async Task Sobreaplicacion_rechaza_SC_003()
    {
        var credit = BuildCredit(30m);
        var m = new Mocks(credit);
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBankDestination());
        m.AccountRepo
            .Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo
            .Setup(r => r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPaymentMethod());
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                DestinationId,
                "TRANSFER",
                50m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        credit.AvailableAmount.Should().Be(30m);
    }

    [Fact]
    public async Task Reintento_con_mismo_ClientRequestId_y_mismo_payload_no_duplica()
    {
        var credit = BuildCredit(100m);
        var m = new Mocks(credit);
        m.DestinationRepo
            .Setup(r => r.GetByIdForShareAsync(TenantId, DestinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBankDestination());
        m.AccountRepo
            .Setup(r =>
                r.GetByIdForShareAsync(TenantId, CompanyId, AccountId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(BuildAccount());
        m.PaymentMethodRepo
            .Setup(r => r.GetByCodeAsync(TenantId, "TRANSFER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPaymentMethod());
        var handler = m.BuildHandler();
        var cri = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var first = await handler.Handle(
            new RegisterSupplierCreditRefundCommand(credit.Id, DestinationId, "TRANSFER", 40m, date, null, cri),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue(first.Error);

        var firstMovement = credit.Movements.Single();
        m.TxRepo
            .Setup(r =>
                r.GetBySupplierCreditMovementIdAsync(TenantId, firstMovement.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                SupplierCreditRefundTransaction.CreateReceived(
                    TenantId,
                    CompanyId,
                    SupplierId,
                    credit.Id,
                    firstMovement.Id,
                    DestinationId,
                    AccountId,
                    "1.1.01",
                    "BANK-01",
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
            new RegisterSupplierCreditRefundCommand(credit.Id, DestinationId, "TRANSFER", 40m, date, null, cri),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue(retry.Error);
        credit.Movements.Should().HaveCount(1, "no debe duplicar el movimiento en un reintento idempotente");
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
}

file static class TestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
