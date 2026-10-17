using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Events;
using ERP.Domain.Modules.Purchases.Events;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Events;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-04 — las 8 altas registran la obligación durable con el hecho económico
/// completo (sin motor, sin cuentas), el intento resuelve destinos/reglas vigentes y falla de forma
/// durable, y las 3 reversas solicitan el espejo del asiento histórico original.
/// </summary>
public sealed class AccountingContinuity04Tests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 16);

    private readonly AccountingContinuityTestHarness _h = new(TenantId, CompanyId);

    private static ICompanyClock Clock() =>
        Mock.Of<ICompanyClock>(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()) == Task.FromResult(Today));

    private AccountingPosting Single(string module, string fact, Guid sourceId)
    {
        var posting = _h.Added.Should().ContainSingle().Subject;
        posting.SourceModule.Should().Be(module);
        posting.FactType.Should().Be(fact);
        posting.SourceEventId.Should().Be(sourceId);
        posting.Status.Should().Be(AccountingPostingStatus.Pending);
        posting.PayloadVersion.Should().Be(AccountingPostingPayload.CurrentVersion);
        _h.Queue.Drain().Should().ContainSingle().Which.Should().Be(posting.Id);
        return posting;
    }

    // ── Altas: obligación durable con el hecho económico ─────────────────────

    [Fact]
    public async Task Venta_mixta_registra_destinos_por_identidad_contado_y_saldo_CxC()
    {
        Guid cash = Guid.NewGuid(), bank = Guid.NewGuid(), card = Guid.NewGuid();
        var e = new SalesInvoiceAuthorizedEvent(Guid.NewGuid(), "001-001-000000010", 117m, UserId, Guid.NewGuid(),
            TenantId, CompanyId, Today.AddDays(-1), 100m, 15m, 0m, 0m, 2m, cashApplied: 100m,
            collectionDestinations:
            [
                new(SalesCollectionDestinationKind.CashRegister, cash, 50m),
                new(SalesCollectionDestinationKind.BankAccount, bank, 30m),
                new(SalesCollectionDestinationKind.PaymentMethod, card, 20m),
            ]);

        await new SalesInvoiceAuthorizedPostingTranslator(_h.Recorder).Handle(e, CancellationToken.None);

        var posting = Single("Sales", "InvoiceIssued", e.InvoiceId);
        posting.EntryDate.Should().Be(e.IssueDate);
        var payload = _h.SinglePayload();
        payload.CashApplied.Should().Be(0m);
        payload.PendingBalance.Should().Be(17m);
        payload.TotalIrbpnr.Should().Be(2m);
        payload.Destinations.Should().BeEquivalentTo(new[]
        {
            new AccountingPostingDestination(AccountingPostingDestination.CashRegister, cash, 50m, AccountNature.Debit),
            new AccountingPostingDestination(AccountingPostingDestination.BankAccount, bank, 30m, AccountNature.Debit),
            new AccountingPostingDestination(AccountingPostingDestination.PaymentMethod, card, 20m, AccountNature.Debit),
        });
    }

    [Fact]
    public async Task Venta_a_credito_no_tiene_destinos_y_todo_el_total_es_CxC()
    {
        var e = new SalesInvoiceAuthorizedEvent(Guid.NewGuid(), "F-2", 115m, UserId, Guid.NewGuid(), TenantId,
            CompanyId, Today, 100m, 15m, 0m, 0m, cashApplied: 0m);

        await new SalesInvoiceAuthorizedPostingTranslator(_h.Recorder).Handle(e, CancellationToken.None);

        var payload = _h.SinglePayload();
        payload.Destinations.Should().BeEmpty();
        payload.PendingBalance.Should().Be(115m);
        payload.CashApplied.Should().Be(0m);
    }

    [Fact]
    public async Task Devolucion_de_venta_registra_obligacion_con_fecha_de_la_Company()
    {
        var e = new SalesReturnAuthorizedEvent(Guid.NewGuid(), Guid.NewGuid(), "DEV-1", 23m, UserId, TenantId,
            CompanyId, 20m, 3m, 0m, 0m, "motivo");

        await new SalesReturnAuthorizedPostingTranslator(_h.Recorder, Clock()).Handle(e, CancellationToken.None);

        Single("Sales", "SalesReturn", e.SalesReturnId).EntryDate.Should().Be(Today);
        _h.SinglePayload().GrandTotal.Should().Be(23m);
    }

    [Fact]
    public async Task Pago_proveedor_parcial_con_anticipo_y_caja_registra_aplicado_remanente_y_medios()
    {
        Guid cash = Guid.NewGuid(), bank = Guid.NewGuid();
        var e = new SupplierPaymentConfirmedEvent(TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 100m, Today,
            [new(null, cash, 60m), new(bank, null, 40m)], appliedAmount: 70m);

        await new SupplierPaymentConfirmedPostingTranslator(_h.Recorder).Handle(e, CancellationToken.None);

        Single("Payables", "SupplierPaymentConfirmed", e.SupplierPaymentId).EntryDate.Should().Be(Today);
        var payload = _h.SinglePayload();
        payload.AppliedToPayableAmount.Should().Be(70m);
        payload.SupplierCreditAmount.Should().Be(30m);
        payload.Destinations.Should().BeEquivalentTo(new[]
        {
            new AccountingPostingDestination(AccountingPostingDestination.CashRegister, cash, 60m, AccountNature.Credit),
            new AccountingPostingDestination(AccountingPostingDestination.BankAccount, bank, 40m, AccountNature.Credit),
        });
    }

    [Fact]
    public async Task Pago_proveedor_anticipo_total_sin_CxP_registra_todo_como_saldo_a_favor()
    {
        var e = new SupplierPaymentConfirmedEvent(TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 50m, Today,
            [new(Guid.NewGuid(), null, 50m)], appliedAmount: 0m);

        await new SupplierPaymentConfirmedPostingTranslator(_h.Recorder).Handle(e, CancellationToken.None);

        var payload = _h.SinglePayload();
        payload.AppliedToPayableAmount.Should().Be(0m);
        payload.SupplierCreditAmount.Should().Be(50m);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Cobro_registra_su_destino_real_como_sustituto_de_la_linea_Debe(bool bank, bool cash)
    {
        var e = new ERP.Domain.Modules.Finance.Events.CollectionAppliedEvent(TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 40m, Today,
            bank ? Guid.NewGuid() : null, cash ? Guid.NewGuid() : null);

        await new CollectionAppliedPostingTranslator(_h.Recorder).Handle(e, CancellationToken.None);

        Single("Finance", "CollectionApplied", e.PaymentId);
        var destinations = _h.SinglePayload().Destinations;
        if (!bank && !cash)
        {
            destinations.Should().BeNull();
            return;
        }
        var destination = destinations.Should().ContainSingle().Subject;
        destination.Kind.Should().Be(bank ? AccountingPostingDestination.BankAccount : AccountingPostingDestination.CashRegister);
        destination.ReferenceId.Should().Be((bank ? e.CompanyBankAccountId : e.CashRegisterId)!.Value);
        destination.OverridesAmountKind.Should().Be(PostingAmountKind.GrandTotal);
        destination.Nature.Should().Be(AccountNature.Debit);
    }

    [Fact]
    public async Task Devolucion_de_compra_conserva_el_hecho_compuesto()
    {
        var e = new PurchaseReturnAuthorizedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            TenantId, CompanyId, "DC-1", UserId, 100m, 12m, 1m, 0m, 113.5m, historicalCostTotal: 105m,
            costVarianceTotal: -5m, appliedToPayableAmount: 80m, supplierCreditAmount: 33.5m, supplierCreditId: Guid.NewGuid(),
            reason: "r", authorizedIrbpnrTotal: 0.5m);

        await new PurchaseReturnAuthorizedPostingTranslator(_h.Recorder, Clock()).Handle(e, CancellationToken.None);

        Single("Purchases", "PurchaseReturn", e.PurchaseReturnId).EntryDate.Should().Be(Today);
        var p = _h.SinglePayload();
        (p.TotalVat, p.TotalIce, p.TotalIrbpnr).Should().Be((12m, 1m, 0.5m));
        (p.AppliedToPayableAmount, p.SupplierCreditAmount, p.HistoricalCostTotal).Should().Be((80m, 33.5m, 105m));
        (p.CostVarianceDebitAmount, p.CostVarianceCreditAmount).Should().Be((0m, 5m));
        p.References.Should().ContainKey("SupplierCreditId");
    }

    [Fact]
    public async Task NC_compra_descuento_registra_su_efecto_propio()
    {
        var e = new PurchaseCreditNoteAuthorizedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            TenantId, CompanyId, "NC-1", UserId, 50m, 7.5m, 58.5m, 58.5m, iceAmount: 0.5m, irbpnrAmount: 0.5m);

        await new PurchaseCreditNoteAuthorizedPostingTranslator(_h.Recorder, Clock()).Handle(e, CancellationToken.None);

        Single("Purchases", "PurchaseCreditNoteAuthorized", e.PurchaseCreditNoteId);
        var p = _h.SinglePayload();
        (p.Subtotal, p.TotalVat, p.TotalIce, p.TotalIrbpnr, p.GrandTotal, p.AppliedToPayableAmount)
            .Should().Be((50m, 7.5m, 0.5m, 0.5m, 58.5m, 58.5m));
    }

    [Fact]
    public async Task Aplicacion_de_saldo_a_favor_usa_el_movimiento_como_identidad()
    {
        var e = new SupplierCreditAppliedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId, CompanyId, 25m, 0m, UserId);

        await new SupplierCreditAppliedPostingTranslator(_h.Recorder, Clock()).Handle(e, CancellationToken.None);

        Single("Purchases", "SupplierCreditApplied", e.SupplierCreditMovementId);
        _h.SinglePayload().GrandTotal.Should().Be(25m);
    }

    [Fact]
    public async Task Reembolso_registra_el_destino_por_identidad_y_la_fecha_efectiva()
    {
        var cash = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        var tx = SupplierCreditRefundTransaction.CreateReceived(TenantId, CompanyId, Guid.NewGuid(), Guid.NewGuid(), movementId,
            null, cash, null, null, "C1", "Caja 1", "CashRegister", "EFE", 30m, "USD", Today.AddDays(-2), UserId, Guid.NewGuid(), "h");
        var repo = new Mock<ISupplierCreditRefundTransactionRepository>();
        repo.Setup(r => r.GetBySupplierCreditMovementIdAsync(TenantId, movementId, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        var e = new SupplierCreditRefundedEvent(tx.SupplierCreditId, movementId, TenantId, CompanyId, 30m, 0m, UserId);

        await new SupplierCreditRefundedPostingTranslator(_h.Recorder, repo.Object).Handle(e, CancellationToken.None);

        Single("Purchases", "SupplierCreditRefunded", movementId).EntryDate.Should().Be(Today.AddDays(-2));
        _h.SinglePayload().Destinations.Should().ContainSingle().Which.Should()
            .Be(new AccountingPostingDestination(AccountingPostingDestination.CashRegister, cash, 30m, AccountNature.Debit, Description: "Caja 1"));
    }

    // ── Política operativa ───────────────────────────────────────────────────

    [Fact]
    public async Task Accounting_Disabled_no_genera_obligacion_y_sin_modo_falla_cerrado()
    {
        var e = new ERP.Domain.Modules.Finance.Events.CollectionAppliedEvent(TenantId, Guid.NewGuid(), CompanyId, Guid.NewGuid(), 40m, Today);
        var disabled = new AccountingContinuityTestHarness(TenantId, CompanyId, AccountingOperationalMode.Disabled);
        await new CollectionAppliedPostingTranslator(disabled.Recorder).Handle(e, CancellationToken.None);
        disabled.Added.Should().BeEmpty();

        var notConfigured = new AccountingContinuityTestHarness(TenantId, CompanyId, mode: null);
        var act = () => new CollectionAppliedPostingTranslator(notConfigured.Recorder).Handle(e, CancellationToken.None);
        await act.Should().ThrowAsync<DomainRuleViolationException>();
    }

    [Fact]
    public async Task Obligacion_existente_no_se_duplica()
    {
        var e = new SupplierCreditAppliedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId, CompanyId, 25m, 0m, UserId);
        _h.Postings
            .Setup(p => p.FindByKeyAsync(TenantId, CompanyId, "Purchases", "SupplierCreditApplied", e.SupplierCreditMovementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountingPosting.CreatePending(TenantId, CompanyId, "Purchases", "SupplierCreditApplied",
                e.SupplierCreditMovementId, null, Today, Today, 2, "{}", UserId, null));

        await new SupplierCreditAppliedPostingTranslator(_h.Recorder, Clock()).Handle(e, CancellationToken.None);

        _h.Added.Should().BeEmpty();
    }

    // ── Reversas: espejo del asiento histórico original ──────────────────────

    [Fact]
    public async Task Tres_reversas_solicitan_el_espejo_del_hecho_original()
    {
        var reversals = new Mock<IAccountingReversalRecorder>();
        var cancelled = new PurchaseReturnCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            TenantId, CompanyId, "DC-1", "anulada", UserId, 80m, 0m, 100m, 0m, 12m, 0m);
        var applicationReversed = new SupplierCreditApplicationReversedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), TenantId, CompanyId, 25m, 25m, UserId);
        var refundReversed = new SupplierCreditRefundReversedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            TenantId, CompanyId, 30m, 30m, UserId);

        await new PurchaseReturnCancelledPostingTranslator(reversals.Object).Handle(cancelled, CancellationToken.None);
        await new SupplierCreditApplicationReversedPostingTranslator(reversals.Object).Handle(applicationReversed, CancellationToken.None);
        await new SupplierCreditRefundReversedPostingTranslator(reversals.Object).Handle(refundReversed, CancellationToken.None);

        reversals.Verify(r => r.RecordSourceAsync(TenantId, CompanyId, "Purchases", "PurchaseReturn",
            cancelled.PurchaseReturnId, "anulada", cancelled.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
        reversals.Verify(r => r.RecordSourceAsync(TenantId, CompanyId, "Purchases", "SupplierCreditApplied",
            applicationReversed.ReversalOfMovementId, It.IsAny<string>(), applicationReversed.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
        reversals.Verify(r => r.RecordSourceAsync(TenantId, CompanyId, "Purchases", "SupplierCreditRefunded",
            refundReversed.ReversalOfMovementId, It.IsAny<string>(), refundReversed.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
        reversals.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancelar_devolucion_en_borrador_no_solicita_reverso()
    {
        var reversals = new Mock<IAccountingReversalRecorder>();
        var e = new PurchaseReturnCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            TenantId, CompanyId, null, "borrador", UserId, null, null, null, null, null, null);

        await new PurchaseReturnCancelledPostingTranslator(reversals.Object).Handle(e, CancellationToken.None);

        reversals.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reversa_de_pago_y_cancelacion_de_NC_conservan_su_reverso_historico()
    {
        var reversals = new Mock<IAccountingReversalRecorder>();
        var payment = new ERP.Domain.Modules.Payables.Events.SupplierPaymentReversedEvent(TenantId, Guid.NewGuid(), CompanyId,
            Guid.NewGuid(), 200m, Today, "error", [new(null, Guid.NewGuid(), 200m)], [new(Guid.NewGuid(), 180m)], appliedAmount: 180m);
        var note = new PurchaseCreditNoteCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            TenantId, CompanyId, "NC", "motivo", UserId, 10m);

        await new SupplierPaymentReversedPostingTranslator(reversals.Object).Handle(payment, CancellationToken.None);
        await new PurchaseCreditNoteCancelledPostingTranslator(reversals.Object).Handle(note, CancellationToken.None);

        reversals.Verify(r => r.RecordSourceAsync(TenantId, CompanyId, "Payables", "SupplierPaymentConfirmed",
            payment.SupplierPaymentId, "error", payment.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
        reversals.Verify(r => r.RecordSourceAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized",
            note.PurchaseCreditNoteId, "motivo", note.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Intento: destinos y reglas con la configuración vigente ──────────────

    private readonly Mock<ICashRegisterRepository> _cash = new();
    private readonly Mock<ICompanyBankAccountRepository> _banks = new();
    private readonly Mock<IPaymentMethodAccountRepository> _methods = new();
    private readonly Mock<IPostingRuleRepository> _rules = new();

    private AccountingPostingFactBuilder Builder() =>
        new(Mock.Of<IExpenseCategoryRepository>(), Mock.Of<IAccountRepository>(), _cash.Object, _banks.Object,
            _methods.Object, _rules.Object,
            Mock.Of<ERP.Domain.Modules.Inventory.Interfaces.IInventoryAdjustmentReasonRepository>(),
            Mock.Of<ERP.Domain.Modules.Inventory.Interfaces.IInventoryAdjustmentReasonAccountRepository>(),
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashMovementReasonRepository>());

    private static AccountingPosting Obligation(string module, string fact, AccountingPostingPayload payload) =>
        AccountingPosting.CreatePending(TenantId, CompanyId, module, fact, Guid.NewGuid(), null, Today, Today,
            AccountingPostingPayload.CurrentVersion, payload.Serialize(), UserId, null);

    private CashRegister CashRegisterWith(Guid? accountId)
    {
        var register = CashRegister.Create(TenantId, CompanyId, Guid.NewGuid(), "C1", "Caja 1", UserId);
        register.SetAccountingAccount(accountId, UserId);
        _cash.Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>())).ReturnsAsync(register);
        return register;
    }

    private CompanyBankAccount BankWith(Guid accountId)
    {
        var bank = CompanyBankAccount.Create(TenantId, CompanyId, Guid.NewGuid(), BankAccountType.Checking, "123456", "Banco", accountId, UserId);
        _banks.Setup(r => r.GetByIdAsync(TenantId, bank.Id, It.IsAny<CancellationToken>())).ReturnsAsync(bank);
        return bank;
    }

    private void RuleWith(string module, string fact, params PostingAmountKind[] kinds)
    {
        var rule = PostingRule.Create(TenantId, CompanyId, module, fact, UserId);
        foreach (var kind in kinds)
            rule.AddLine(Guid.NewGuid(), AccountNature.Debit, kind);
        _rules.Setup(r => r.FindByKeyAsync(TenantId, CompanyId, module, fact, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
    }

    [Fact]
    public async Task Intento_de_venta_mixta_resuelve_cuentas_vigentes_y_agrupa_por_cuenta()
    {
        Guid cashAccount = Guid.NewGuid(), bankAccount = Guid.NewGuid(), cardAccount = Guid.NewGuid(), card = Guid.NewGuid();
        var register = CashRegisterWith(cashAccount);
        var bank = BankWith(bankAccount);
        _methods.Setup(r => r.GetAsync(TenantId, CompanyId, card, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentMethodAccount.Create(TenantId, CompanyId, card, cardAccount, UserId));
        var payload = new AccountingPostingPayload(100m, 15m, 0m, 0m, 115m, CashApplied: 0m, PendingBalance: 15m,
            Destinations:
            [
                new(AccountingPostingDestination.CashRegister, register.Id, 40m, AccountNature.Debit),
                new(AccountingPostingDestination.CashRegister, register.Id, 10m, AccountNature.Debit),
                new(AccountingPostingDestination.BankAccount, bank.Id, 30m, AccountNature.Debit),
                new(AccountingPostingDestination.PaymentMethod, card, 20m, AccountNature.Debit),
            ]);

        var fact = await Builder().BuildAsync(Obligation("Sales", "InvoiceIssued", payload), CancellationToken.None);

        fact.IsSuccess.Should().BeTrue(fact.Error);
        fact.Value!.PendingBalance.Should().Be(15m);
        fact.Value.Allocations!.Select(a => (a.AccountingAccountId, a.Amount, a.Nature)).Should().BeEquivalentTo(new[]
        {
            (cashAccount, 50m, AccountNature.Debit),
            (bankAccount, 30m, AccountNature.Debit),
            (cardAccount, 20m, AccountNature.Debit),
        });
    }

    [Fact]
    public async Task Intento_falla_durable_sin_cuenta_de_caja_o_de_medio_y_tras_configurarla_construye_el_hecho()
    {
        var register = CashRegisterWith(null);
        var card = Guid.NewGuid();
        var cashPayload = new AccountingPostingPayload(0m, 0m, 0m, 0m, 40m,
            Destinations: [new(AccountingPostingDestination.CashRegister, register.Id, 40m, AccountNature.Debit, PostingAmountKind.GrandTotal)]);
        var cardPayload = new AccountingPostingPayload(0m, 0m, 0m, 0m, 20m,
            Destinations: [new(AccountingPostingDestination.PaymentMethod, card, 20m, AccountNature.Debit)]);
        var cashPosting = Obligation("Finance", "CollectionApplied", cashPayload);

        (await Builder().BuildAsync(cashPosting, CancellationToken.None)).Code
            .Should().Be(AccountingPostingFactBuilder.DestinationAccountNotConfiguredCode);
        (await Builder().BuildAsync(Obligation("Sales", "InvoiceIssued", cardPayload), CancellationToken.None)).Code
            .Should().Be(AccountingPostingFactBuilder.DestinationAccountNotConfiguredCode);

        var account = Guid.NewGuid();
        register.SetAccountingAccount(account, UserId);
        var fact = await Builder().BuildAsync(cashPosting, CancellationToken.None);
        fact.IsSuccess.Should().BeTrue(fact.Error);
        (fact.Value!.OverrideAccountId, fact.Value.OverrideAmountKind, fact.Value.OverrideAccountNature)
            .Should().Be((account, PostingAmountKind.GrandTotal, AccountNature.Debit));
    }

    [Fact]
    public async Task Destino_de_otra_Company_no_se_resuelve()
    {
        var foreign = CashRegister.Create(TenantId, Guid.NewGuid(), Guid.NewGuid(), "X", "Ajena", UserId);
        foreign.SetAccountingAccount(Guid.NewGuid(), UserId);
        _cash.Setup(r => r.GetByIdAsync(TenantId, foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);
        var payload = new AccountingPostingPayload(0m, 0m, 0m, 0m, 10m,
            Destinations: [new(AccountingPostingDestination.CashRegister, foreign.Id, 10m, AccountNature.Debit)]);

        (await Builder().BuildAsync(Obligation("Finance", "CollectionApplied", payload), CancellationToken.None)).Code
            .Should().Be(AccountingPostingFactBuilder.DestinationNotFoundCode);
    }

    [Fact]
    public async Task Anticipo_sin_linea_de_anticipos_en_la_regla_falla_y_con_ella_construye_el_hecho()
    {
        var bank = BankWith(Guid.NewGuid());
        var payload = new AccountingPostingPayload(0m, 0m, 0m, 0m, 100m, AppliedToPayableAmount: 70m, SupplierCreditAmount: 30m,
            Destinations: [new(AccountingPostingDestination.BankAccount, bank.Id, 100m, AccountNature.Credit)]);
        var posting = Obligation("Payables", "SupplierPaymentConfirmed", payload);

        RuleWith("Payables", "SupplierPaymentConfirmed", PostingAmountKind.GrandTotal);
        (await Builder().BuildAsync(posting, CancellationToken.None)).Code
            .Should().Be(AccountingPostingFactBuilder.RuleLineNotConfiguredCode);

        RuleWith("Payables", "SupplierPaymentConfirmed", PostingAmountKind.AppliedToPayable, PostingAmountKind.SupplierCredit);
        var fact = await Builder().BuildAsync(posting, CancellationToken.None);
        fact.IsSuccess.Should().BeTrue(fact.Error);
        (fact.Value!.AppliedToPayableAmount, fact.Value.SupplierCreditAmount).Should().Be((70m, 30m));
        fact.Value.Allocations.Should().ContainSingle().Which.Nature.Should().Be(AccountNature.Credit);
    }

    [Fact]
    public async Task IRBPNR_sin_linea_en_la_regla_falla_en_el_intento()
    {
        RuleWith("Sales", "SalesReturn", PostingAmountKind.Subtotal, PostingAmountKind.TaxVat);
        var posting = Obligation("Sales", "SalesReturn", new AccountingPostingPayload(10m, 1.5m, 0m, 0m, 12m, TotalIrbpnr: 0.5m));

        (await Builder().BuildAsync(posting, CancellationToken.None)).Code
            .Should().Be(AccountingPostingFactBuilder.RuleLineNotConfiguredCode);
    }

    [Fact]
    public async Task Devolucion_de_compra_reconstruye_todos_los_componentes()
    {
        var payload = new AccountingPostingPayload(0m, 12m, 1m, 0m, 0m, TotalIrbpnr: 0.5m, AppliedToPayableAmount: 80m,
            SupplierCreditAmount: 33.5m, CostVarianceDebitAmount: 0m, CostVarianceCreditAmount: 5m, HistoricalCostTotal: 105m);
        RuleWith("Purchases", "PurchaseReturn", PostingAmountKind.TaxIrbpnr, PostingAmountKind.SupplierCredit);

        var fact = (await Builder().BuildAsync(Obligation("Purchases", "PurchaseReturn", payload), CancellationToken.None)).Value!;

        (fact.TotalVat, fact.TotalIce, fact.TotalIrbpnr, fact.AppliedToPayableAmount, fact.SupplierCreditAmount,
            fact.CostVarianceDebitAmount, fact.CostVarianceCreditAmount, fact.HistoricalCostTotal)
            .Should().Be((12m, 1m, 0.5m, 80m, 33.5m, 0m, 5m, 105m));
    }

    [Fact]
    public async Task Payload_version_1_sigue_siendo_legible()
    {
        var v1 = AccountingPosting.CreatePending(TenantId, CompanyId, "Purchases", "InvoiceReceived", Guid.NewGuid(), null,
            Today, Today, 1, new AccountingPostingPayload(100m, 15m, 0m, 0m, 115m).Serialize(), UserId, null);

        var fact = await Builder().BuildAsync(v1, CancellationToken.None);

        fact.IsSuccess.Should().BeTrue();
        fact.Value!.GrandTotal.Should().Be(115m);
    }
}
