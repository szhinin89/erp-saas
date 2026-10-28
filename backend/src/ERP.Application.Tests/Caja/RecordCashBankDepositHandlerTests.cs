using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// R3 (2026-10-28) — el depósito bancario es una salida del efectivo del turno: nunca puede superar
/// el efectivo esperado de la sesión (<see cref="CashSession.CurrentBalance"/>), igual que el pago a
/// proveedor en efectivo. Reproducido en E2E: un depósito de 100.00 con 16.68 en caja dejaba el
/// esperado en −83.32 y un asiento Banco/Caja sin efectivo real.
/// </summary>
public sealed class RecordCashBankDepositHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CashSession OpenSession() =>
        CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            Guid.NewGuid(),
            "CAJA-01",
            "Caja Principal",
            Guid.NewGuid(),
            "001",
            100m,
            UserId
        );

    private static CompanyBankAccount BankAccount() =>
        CompanyBankAccount.Create(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            BankAccountType.Checking,
            "2100456789",
            "Banco Corriente",
            Guid.NewGuid(),
            UserId
        );

    private static (RecordCashBankDepositHandler Handler, Mock<ICashSessionRepository> Repo) Build(
        CashSession session,
        CompanyBankAccount bank
    )
    {
        var repo = new Mock<ICashSessionRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var banks = new Mock<ICompanyBankAccountRepository>();
        banks.Setup(b => b.GetByIdAsync(TenantId, bank.Id, It.IsAny<CancellationToken>())).ReturnsAsync(bank);
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        user.Setup(u => u.FullName).Returns("Cajero Test");

        var handler = new RecordCashBankDepositHandler(
            repo.Object,
            banks.Object,
            new Mock<IUnitOfWork>().Object,
            tenant.Object,
            branch.Object,
            user.Object
        );
        return (handler, repo);
    }

    private static RecordCashBankDepositCommand Deposit(Guid sessionId, Guid bankId, decimal amount) =>
        new(sessionId, bankId, amount, "Depósito de prueba", "PAP-1", Guid.NewGuid());

    [Fact]
    public async Task Deposito_mayor_al_efectivo_esperado_es_rechazado_sin_registrar()
    {
        var session = OpenSession(); // apertura 100.00
        var bank = BankAccount();
        var (handler, repo) = Build(session, bank);

        var result = await handler.Handle(Deposit(session.Id, bank.Id, 100.01m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La caja dispone de $100.00 y se intenta registrar una salida de $100.01.");
        session.Movements.Should().NotContain(m => m.MovementType == CashMovementType.BankDeposit);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deposito_igual_al_efectivo_esperado_se_registra()
    {
        var session = OpenSession();
        var bank = BankAccount();
        var (handler, repo) = Build(session, bank);

        var result = await handler.Handle(Deposit(session.Id, bank.Id, 100m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        session.CurrentBalance.Should().Be(0m);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
