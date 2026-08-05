using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// P0-02 Fase 8 — ReverseSupplierCreditRefundHandler: reversa feliz banco/caja con herencia exacta
/// campo por campo, SC-001, SC-011, SC-027 (caja sin sesión activa al revertir), idempotencia.
/// </summary>
public sealed class ReverseSupplierCreditRefundUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid DestinationId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();

    private sealed record Fixture(
        SupplierCredit Credit,
        SupplierCreditRefundTransaction OriginalTx,
        Guid RefundMovementId
    );

    private static Fixture BuildBankFixture(decimal creditAmount = 100m, decimal refundAmount = 40m)
    {
        var credit = SupplierCredit.CreateFromReturn(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "USD",
            Guid.NewGuid(),
            creditAmount,
            UserId
        );
        var registerHash = RegisterSupplierCreditRefundHandler.ComputeRegisterPayloadHash(
            credit.Id,
            DestinationId,
            "TRANSFER",
            refundAmount,
            "USD",
            DateOnly.FromDateTime(DateTime.UtcNow),
            null
        );
        var movement = credit.RegisterRefund(refundAmount, UserId, Guid.NewGuid(), registerHash);
        var tx = SupplierCreditRefundTransaction.CreateReceived(
            TenantId,
            CompanyId,
            SupplierId,
            credit.Id,
            movement.Id,
            DestinationId,
            AccountId,
            "1.1.01",
            "BANK-01",
            "Banco Pichincha CTE",
            "BankAccount",
            "TRANSFER",
            refundAmount,
            "USD",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            movement.ClientRequestId,
            registerHash
        );
        return new Fixture(credit, tx, movement.Id);
    }

    private static Fixture BuildCashFixture(
        Guid cashSessionId,
        decimal creditAmount = 100m,
        decimal refundAmount = 40m
    )
    {
        var credit = SupplierCredit.CreateFromReturn(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "USD",
            Guid.NewGuid(),
            creditAmount,
            UserId
        );
        var registerHash = RegisterSupplierCreditRefundHandler.ComputeRegisterPayloadHash(
            credit.Id,
            DestinationId,
            "CASH",
            refundAmount,
            "USD",
            DateOnly.FromDateTime(DateTime.UtcNow),
            null
        );
        var movement = credit.RegisterRefund(refundAmount, UserId, Guid.NewGuid(), registerHash);
        var tx = SupplierCreditRefundTransaction.CreateReceived(
            TenantId,
            CompanyId,
            SupplierId,
            credit.Id,
            movement.Id,
            DestinationId,
            AccountId,
            "1.1.01",
            "CASH-01",
            "Caja Matriz",
            "CashRegister",
            "CASH",
            refundAmount,
            "USD",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            movement.ClientRequestId,
            registerHash,
            cashSessionId: cashSessionId,
            cashMovementId: Guid.NewGuid()
        );
        return new Fixture(credit, tx, movement.Id);
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
        public Mock<ICashSessionRepository> CashSessionRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public Mocks(Fixture f)
        {
            CreditRepo
                .Setup(r => r.GetByIdAsync(TenantId, f.Credit.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Credit);
            TxRepo
                .Setup(r =>
                    r.GetByIdForShareAsync(TenantId, f.OriginalTx.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(f.OriginalTx);
            Uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        }

        public ReverseSupplierCreditRefundHandler BuildHandler() =>
            new(
                CreditRepo.Object,
                TxRepo.Object,
                CashSessionRepo.Object,
                Uow.Object,
                DbEx.Object,
                new FixedCurrentTenant(),
                new FixedCurrentUser()
            );
    }

    [Fact]
    public async Task Reversa_feliz_banco_restituye_AvailableAmount()
    {
        var f = BuildBankFixture(creditAmount: 100m, refundAmount: 40m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                f.Credit.Id,
                f.OriginalTx.Id,
                "Reembolso duplicado por error",
                DateOnly.FromDateTime(DateTime.UtcNow),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Credit.AvailableAmount.Should().Be(100m);
    }

    [Fact]
    public async Task Reversa_hereda_campo_por_campo_del_original()
    {
        var f = BuildBankFixture(refundAmount: 55m);
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                f.Credit.Id,
                f.OriginalTx.Id,
                "Motivo de reversa",
                DateOnly.FromDateTime(DateTime.UtcNow),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var dto = result.Value!;
        dto.OriginalTransactionId.Should().Be(f.OriginalTx.Id);
        dto.FinancialDestinationId.Should().Be(f.OriginalTx.FinancialDestinationId);
        dto.AccountingAccountId.Should().Be(f.OriginalTx.AccountingAccountId);
        dto.PaymentMethodCode.Should().Be(f.OriginalTx.PaymentMethodCode);
        dto.Amount.Should().Be(f.OriginalTx.Amount);
        dto.CurrencyCode.Should().Be(f.OriginalTx.CurrencyCode);
        dto.ExternalReference.Should()
            .BeNull("la fila de reversa nunca lleva ExternalReference (§6.4quinquies)");
        dto.Reason.Should().Be("Motivo de reversa");
    }

    [Fact]
    public async Task Reversa_en_caja_con_sesion_activa_vincula_CashMovement_compensatorio()
    {
        var session = BuildOpenCashSession();
        var f = BuildCashFixture(session.Id, refundAmount: 40m);
        var m = new Mocks(f);
        m.CashSessionRepo.Setup(r =>
                r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(session);
        m.CashSessionRepo.Setup(r =>
                r.GetOpenByCashRegisterForShareAsync(
                    TenantId,
                    CashRegisterId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(session);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                f.Credit.Id,
                f.OriginalTx.Id,
                "Motivo",
                DateOnly.FromDateTime(DateTime.UtcNow),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CashMovementId.Should().NotBeNull();
        session
            .Movements.Should()
            .HaveCount(2, "apertura + el movimiento compensatorio de la reversa");
    }

    [Fact]
    public async Task Caja_sin_sesion_activa_al_revertir_rechaza_SC_027_sin_mutar_el_original()
    {
        var f = BuildCashFixture(Guid.NewGuid(), refundAmount: 40m);
        var m = new Mocks(f);
        var closedSession = BuildOpenCashSession();
        m.CashSessionRepo.Setup(r =>
                r.GetByIdAsync(
                    TenantId,
                    f.OriginalTx.CashSessionId!.Value,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(closedSession);
        m.CashSessionRepo.Setup(r =>
                r.GetOpenByCashRegisterForShareAsync(
                    TenantId,
                    CashRegisterId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((CashSession?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                f.Credit.Id,
                f.OriginalTx.Id,
                "Motivo",
                DateOnly.FromDateTime(DateTime.UtcNow),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sesión de caja");
        f.Credit.AvailableAmount.Should()
            .Be(60m, "el crédito no debe mutar cuando la reversa queda bloqueada");
    }

    [Fact]
    public async Task Credito_inexistente_rechaza_SC_001()
    {
        var f = BuildBankFixture();
        var m = new Mocks(f);
        var missingId = Guid.NewGuid();
        m.CreditRepo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierCredit?)null);
        var handler = m.BuildHandler();

        var result = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                missingId,
                f.OriginalTx.Id,
                "Motivo",
                DateOnly.FromDateTime(DateTime.UtcNow),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Segunda_reversa_del_mismo_original_rechaza_SC_011()
    {
        var f = BuildBankFixture();
        var m = new Mocks(f);
        var handler = m.BuildHandler();

        var first = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                f.Credit.Id,
                f.OriginalTx.Id,
                "Primera reversa",
                DateOnly.FromDateTime(DateTime.UtcNow),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue(first.Error);

        var second = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                f.Credit.Id,
                f.OriginalTx.Id,
                "Segunda reversa",
                DateOnly.FromDateTime(DateTime.UtcNow),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        second.IsSuccess.Should().BeFalse();
        f.Credit.Movements.Should()
            .HaveCount(2, "1 refund (fixture) + 1 reversa — la segunda no debe agregar nada");
    }

    [Fact]
    public async Task Reintento_con_mismo_ClientRequestId_no_duplica()
    {
        var f = BuildBankFixture();
        var m = new Mocks(f);
        var handler = m.BuildHandler();
        var cri = Guid.NewGuid();

        var first = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                f.Credit.Id,
                f.OriginalTx.Id,
                "Motivo",
                DateOnly.FromDateTime(DateTime.UtcNow),
                cri
            ),
            CancellationToken.None
        );
        first.IsSuccess.Should().BeTrue(first.Error);

        var reversalMovement = f.Credit.Movements.Single(x => x.Id != f.RefundMovementId);
        m.TxRepo.Setup(r =>
                r.GetBySupplierCreditMovementIdAsync(
                    TenantId,
                    reversalMovement.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                SupplierCreditRefundTransaction.CreateReversal(
                    f.OriginalTx,
                    reversalMovement.Id,
                    "Motivo",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    UserId,
                    cri,
                    reversalMovement.RequestPayloadHash
                )
            );

        var retry = await handler.Handle(
            new ReverseSupplierCreditRefundCommand(
                f.Credit.Id,
                f.OriginalTx.Id,
                "Motivo",
                DateOnly.FromDateTime(DateTime.UtcNow),
                cri
            ),
            CancellationToken.None
        );

        retry.IsSuccess.Should().BeTrue(retry.Error);
        f.Credit.Movements.Should().HaveCount(2);
    }

    private sealed class FixedCurrentTenant : ICurrentTenant
    {
        public Guid TenantId => ReverseSupplierCreditRefundUseCasesTests.TenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public Guid UserId => ReverseSupplierCreditRefundUseCasesTests.UserId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }
}
