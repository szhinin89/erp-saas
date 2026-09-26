using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Finance.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — forma del <see cref="PostingFact"/> de reembolso y
/// reversa: FactType canónico único (nunca uno por destino), Haber/Debe Anticipos vía la regla
/// (GrandTotal) y Caja/Banco vía allocation con la cuenta CONGELADA de la transacción; fail-closed
/// (excepción, nunca warning) si el posting falla o falta la transacción. El asiento real (balance,
/// cuentas, rollback) se valida en Postgres en <c>SupplierCreditRefundPostingIntegrationTests</c>.
/// </summary>
public sealed class SupplierCreditRefundPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid DestinationAccountId = Guid.NewGuid();
    private static readonly DateOnly EffectiveDate = new(2026, 9, 20);

    private static SupplierCreditRefundTransaction Received(Guid movementId, decimal amount) =>
        SupplierCreditRefundTransaction.CreateReceived(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            movementId,
            Guid.NewGuid(),
            null,
            DestinationAccountId,
            "1.1.02.001",
            "1234567890",
            "Banco Pichincha CTE",
            "BankAccount",
            "TRANSFER",
            amount,
            "USD",
            EffectiveDate,
            UserId,
            Guid.NewGuid(),
            "hash"
        );

    private static (Mock<IPostingEngine> Engine, List<PostingFact> Facts) Engine(bool succeed = true)
    {
        var facts = new List<PostingFact>();
        var engine = new Mock<IPostingEngine>();
        engine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((f, _) => facts.Add(f))
            .ReturnsAsync(
                succeed
                    ? Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created))
                    : Result<PostingOutcomeDto>.Failure("No existe regla contable.", "RULE_NOT_FOUND")
            );
        return (engine, facts);
    }

    private static Mock<ISupplierCreditRefundTransactionRepository> Repo(Guid movementId, SupplierCreditRefundTransaction? tx)
    {
        var repo = new Mock<ISupplierCreditRefundTransactionRepository>();
        repo.Setup(r => r.GetBySupplierCreditMovementIdAsync(TenantId, movementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx);
        return repo;
    }

    [Fact]
    public async Task Reembolso_publica_FactType_canonico_con_Debe_dinamico_a_la_cuenta_congelada_del_destino()
    {
        var movementId = Guid.NewGuid();
        var (engine, facts) = Engine();
        var evt = new SupplierCreditRefundedEvent(Guid.NewGuid(), movementId, TenantId, CompanyId, 35m, 65m, UserId);

        await new SupplierCreditRefundedPostingTranslator(engine.Object, Repo(movementId, Received(movementId, 35m)).Object)
            .Handle(evt, CancellationToken.None);

        var fact = facts.Should().ContainSingle().Subject;
        fact.SourceModule.Should().Be("Purchases");
        fact.FactType.Should().Be("SupplierCreditRefunded", "un único FactType, nunca uno por caja/banco");
        fact.SourceEventId.Should().Be(movementId);
        fact.EntryDate.Should().Be(EffectiveDate);
        fact.GrandTotal.Should().Be(35m, "la regla acredita Anticipos por GrandTotal");
        var allocation = fact.Allocations.Should().ContainSingle().Subject;
        allocation.AccountingAccountId.Should().Be(DestinationAccountId);
        allocation.Nature.Should().Be(AccountNature.Debit);
        allocation.Amount.Should().Be(35m);
    }

    [Fact]
    public async Task Reversa_publica_espejo_con_Haber_dinamico_a_la_misma_cuenta_heredada()
    {
        var reversalMovementId = Guid.NewGuid();
        var original = Received(Guid.NewGuid(), 35m);
        var reversal = SupplierCreditRefundTransaction.CreateReversal(
            original, reversalMovementId, "Rechazado por el banco", EffectiveDate.AddDays(2), UserId, Guid.NewGuid(), "hash-r");
        var (engine, facts) = Engine();
        var evt = new SupplierCreditRefundReversedEvent(
            Guid.NewGuid(), reversalMovementId, Guid.NewGuid(), TenantId, CompanyId, 35m, 100m, UserId);

        await new SupplierCreditRefundReversedPostingTranslator(engine.Object, Repo(reversalMovementId, reversal).Object)
            .Handle(evt, CancellationToken.None);

        var fact = facts.Should().ContainSingle().Subject;
        fact.FactType.Should().Be("SupplierCreditRefundReversed");
        fact.GrandTotal.Should().Be(35m);
        fact.EntryDate.Should().Be(EffectiveDate.AddDays(2));
        var allocation = fact.Allocations.Should().ContainSingle().Subject;
        allocation.AccountingAccountId.Should().Be(DestinationAccountId, "nunca se resuelve la cuenta vigente del destino");
        allocation.Nature.Should().Be(AccountNature.Credit);
    }

    [Fact]
    public async Task Posting_fallido_lanza_excepcion_fail_closed_nunca_solo_log()
    {
        var movementId = Guid.NewGuid();
        var (engine, _) = Engine(succeed: false);
        var evt = new SupplierCreditRefundedEvent(Guid.NewGuid(), movementId, TenantId, CompanyId, 10m, 90m, UserId);

        var act = () => new SupplierCreditRefundedPostingTranslator(engine.Object, Repo(movementId, Received(movementId, 10m)).Object)
            .Handle(evt, CancellationToken.None);

        (await act.Should().ThrowAsync<SupplierCreditRefundPostingFailedException>())
            .Which.Code.Should().Be("RULE_NOT_FOUND");
    }

    [Fact]
    public async Task Transaccion_inexistente_lanza_excepcion_sin_postear()
    {
        var movementId = Guid.NewGuid();
        var (engine, facts) = Engine();
        var evt = new SupplierCreditRefundReversedEvent(
            Guid.NewGuid(), movementId, Guid.NewGuid(), TenantId, CompanyId, 10m, 100m, UserId);

        var act = () => new SupplierCreditRefundReversedPostingTranslator(engine.Object, Repo(movementId, null).Object)
            .Handle(evt, CancellationToken.None);

        await act.Should().ThrowAsync<SupplierCreditRefundPostingFailedException>();
        facts.Should().BeEmpty();
    }
}
