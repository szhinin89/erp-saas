using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C (ADR-035) — posting del remanente no aplicado:
/// Debe CxP (AppliedToPayable) + Debe Anticipos (SupplierCredit) = Haber fuentes (total); guard
/// fail-closed si la regla de la empresa no declara la línea de anticipos; reversa espejo. El
/// balance real del asiento (PostingEngine + JournalFactory) se valida en Postgres en
/// <c>SupplierPaymentEndToEndTests</c>.
/// </summary>
public sealed class SupplierPaymentUnappliedAdvancePostingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly PaymentDate = new(2026, 8, 28);

    private static CashRegister Register()
    {
        var register = CashRegister.Create(TenantId, CompanyId, BranchId, $"C-{Guid.NewGuid():N}"[..8], "Caja", UserId);
        register.SetAccountingAccount(Guid.NewGuid(), UserId);
        return register;
    }

    private static (Mock<IPostingEngine> engine, Mock<ICashRegisterRepository> registers, CashRegister register) Arrange(
        bool advanceLineConfigured,
        string factType
    )
    {
        var engine = new Mock<IPostingEngine>();
        engine
            .Setup(e => e.IsAmountKindConfiguredAsync(TenantId, CompanyId, "Payables", factType, PostingAmountKind.SupplierCredit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advanceLineConfigured);
        engine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));
        var register = Register();
        var registers = new Mock<ICashRegisterRepository>();
        registers.Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())).ReturnsAsync(register);
        return (engine, registers, register);
    }

    [Fact]
    public async Task Confirmacion_con_remanente_transporta_Applied_y_SupplierCredit_y_Haber_por_el_total()
    {
        var (engine, registers, register) = Arrange(advanceLineConfigured: true, "SupplierPaymentConfirmed");
        PostingFact? captured = null;
        engine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));
        var evt = new SupplierPaymentConfirmedEvent(
            TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 200m, PaymentDate,
            [new SupplierPaymentConfirmedMethodLine(null, register.Id, 200m)],
            appliedAmount: 180m
        );

        await new SupplierPaymentConfirmedPostingTranslator(engine.Object, Mock.Of<ICompanyBankAccountRepository>(), registers.Object)
            .Handle(evt, CancellationToken.None);

        captured!.AppliedToPayableAmount.Should().Be(180m);
        captured.SupplierCreditAmount.Should().Be(20m);
        captured.GrandTotal.Should().Be(200m);
        captured.Allocations!.Sum(a => a.Amount).Should().Be(200m);
        captured.Allocations!.Should().OnlyContain(a => a.Nature == AccountNature.Credit);
    }

    [Fact]
    public async Task Confirmacion_con_remanente_y_regla_sin_linea_de_anticipos_falla_cerrado_sin_postear()
    {
        var (engine, registers, register) = Arrange(advanceLineConfigured: false, "SupplierPaymentConfirmed");
        var evt = new SupplierPaymentConfirmedEvent(
            TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 200m, PaymentDate,
            [new SupplierPaymentConfirmedMethodLine(null, register.Id, 200m)],
            appliedAmount: 180m
        );

        var act = () => new SupplierPaymentConfirmedPostingTranslator(engine.Object, Mock.Of<ICompanyBankAccountRepository>(), registers.Object)
            .Handle(evt, CancellationToken.None);

        await act.Should().ThrowAsync<SupplierPaymentPostingFailedException>().WithMessage("*anticipos a proveedores*");
        engine.Verify(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmacion_sin_remanente_no_exige_linea_de_anticipos()
    {
        var (engine, registers, register) = Arrange(advanceLineConfigured: false, "SupplierPaymentConfirmed");
        var evt = new SupplierPaymentConfirmedEvent(
            TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 200m, PaymentDate,
            [new SupplierPaymentConfirmedMethodLine(null, register.Id, 200m)]
        );

        await new SupplierPaymentConfirmedPostingTranslator(engine.Object, Mock.Of<ICompanyBankAccountRepository>(), registers.Object)
            .Handle(evt, CancellationToken.None);

        engine.Verify(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()), Times.Once);
        engine.Verify(
            e => e.IsAmountKindConfiguredAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PostingAmountKind>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Reversa_con_remanente_es_espejo_y_aplica_el_mismo_guard()
    {
        var (engine, registers, register) = Arrange(advanceLineConfigured: true, "SupplierPaymentReversed");
        PostingFact? captured = null;
        engine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));
        var evt = new SupplierPaymentReversedEvent(
            TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 200m, PaymentDate, "No ejecutada",
            [new SupplierPaymentConfirmedMethodLine(null, register.Id, 200m)],
            [new SupplierPaymentReversedApplicationLine(Guid.NewGuid(), 180m)],
            appliedAmount: 180m
        );

        await new SupplierPaymentReversedPostingTranslator(engine.Object, Mock.Of<ICompanyBankAccountRepository>(), registers.Object)
            .Handle(evt, CancellationToken.None);

        captured!.AppliedToPayableAmount.Should().Be(180m);
        captured.SupplierCreditAmount.Should().Be(20m);
        captured.Allocations!.Should().OnlyContain(a => a.Nature == AccountNature.Debit);

        var (engineOff, registersOff, registerOff) = Arrange(advanceLineConfigured: false, "SupplierPaymentReversed");
        var evtOff = new SupplierPaymentReversedEvent(
            TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 200m, PaymentDate, "x",
            [new SupplierPaymentConfirmedMethodLine(null, registerOff.Id, 200m)],
            [new SupplierPaymentReversedApplicationLine(Guid.NewGuid(), 180m)],
            appliedAmount: 180m
        );
        var act = () => new SupplierPaymentReversedPostingTranslator(engineOff.Object, Mock.Of<ICompanyBankAccountRepository>(), registersOff.Object)
            .Handle(evtOff, CancellationToken.None);
        await act.Should().ThrowAsync<SupplierPaymentPostingFailedException>();
    }
}
