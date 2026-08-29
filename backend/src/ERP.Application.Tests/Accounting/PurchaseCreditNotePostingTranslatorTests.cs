using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CREDIT-NOTES-POSTING-08 — <see cref="PurchaseCreditNoteAuthorizedPostingTranslator"/>
/// (FactType "PurchaseCreditNoteAuthorized", asiento nuevo balanceado) y
/// <see cref="PurchaseCreditNoteCancelledPostingTranslator"/> (reversa el asiento original vía
/// <see cref="ReverseJournalEntryCommand"/>, mismo criterio que
/// <c>PurchaseInvoiceCancelledPostingTranslator</c> de ACCOUNTING-REVERSALS-05).
/// </summary>
public sealed class PurchaseCreditNotePostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static PurchaseCreditNoteAuthorizedEvent AuthorizedEvent(
        Guid creditNoteId,
        decimal subtotal = 100m,
        decimal vat = 15m,
        decimal appliedToPayable = 115m,
        decimal iceAmount = 0m,
        decimal irbpnrAmount = 0m
    ) =>
        new(
            creditNoteId,
            Guid.NewGuid(),
            SupplierId,
            BranchId,
            TenantId,
            CompanyId,
            "NC-001-001-000000005",
            CreatedBy,
            subtotal,
            vat,
            subtotal + vat + iceAmount + irbpnrAmount,
            appliedToPayable,
            iceAmount,
            irbpnrAmount
        );

    private static PurchaseCreditNoteCancelledEvent CancelledEvent(
        Guid creditNoteId,
        decimal? appliedToPayable
    ) =>
        new(
            creditNoteId,
            Guid.NewGuid(),
            SupplierId,
            BranchId,
            TenantId,
            CompanyId,
            "NC-001-001-000000005",
            "Error de digitación",
            CreatedBy,
            appliedToPayable
        );

    // ── PurchaseCreditNoteAuthorizedPostingTranslator (pipeline real) ──────

    private static Account PostableAccount(string code, string name) =>
        Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            name,
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            CreatedBy
        );

    private static AccountingPeriod OpenPeriod() =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            2026,
            8,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            CreatedBy
        );

    private static PostingRule Rule(Guid payableId, Guid expenseId, Guid vatId)
    {
        var rule = PostingRule.Create(
            TenantId,
            CompanyId,
            "Purchases",
            "PurchaseCreditNoteAuthorized",
            null,
            null,
            null,
            CreatedBy
        );
        rule.AddLine(payableId, AccountNature.Debit, PostingAmountKind.AppliedToPayable);
        rule.AddLine(expenseId, AccountNature.Credit, PostingAmountKind.Subtotal);
        rule.AddLine(vatId, AccountNature.Credit, PostingAmountKind.TaxVat);
        return rule;
    }

    /// <summary>ACCOUNTING-PURCHASE-CREDIT-NOTE-ICE-08B — misma regla con una línea ICE adicional. La MISMA regla sirve para NC con y sin ICE: si IceAmount=0, JournalFactory omite esta línea automáticamente (nunca contabiliza monto cero).</summary>
    private static PostingRule RuleWithIce(Guid payableId, Guid expenseId, Guid vatId, Guid iceId)
    {
        var rule = Rule(payableId, expenseId, vatId);
        rule.AddLine(iceId, AccountNature.Credit, PostingAmountKind.TaxIce);
        return rule;
    }

    /// <summary>TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E — misma regla con una línea IRBPNR adicional, mismo criterio aditivo que <see cref="RuleWithIce"/>.</summary>
    private static PostingRule RuleWithIrbpnr(Guid payableId, Guid expenseId, Guid vatId, Guid irbpnrId)
    {
        var rule = Rule(payableId, expenseId, vatId);
        rule.AddLine(irbpnrId, AccountNature.Credit, PostingAmountKind.TaxIrbpnr);
        return rule;
    }

    private sealed class EngineMocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<IAccountingPeriodRepository> AccountingPeriods { get; } = new();
        public Mock<IJournalEntrySequenceRepository> JournalEntrySequences { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public JournalEntry? Captured { get; private set; }

        public EngineMocks()
        {
            JournalEntries
                .Setup(r =>
                    r.AcquireIdempotencyLockAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);
            JournalEntries
                .Setup(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
                .Callback<JournalEntry, CancellationToken>((e, _) => Captured = e)
                .Returns(Task.CompletedTask);
            AccountingPeriods
                .Setup(r =>
                    r.FindContainingDateAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<DateOnly>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(OpenPeriod());
            JournalEntrySequences
                .Setup(r =>
                    r.ReserveNextNumberAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(1);
        }

        public void RegisterAccount(Account account) =>
            Accounts
                .Setup(r =>
                    r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(account);

        public PostingEngine BuildEngine() =>
            new(
                JournalEntries.Object,
                PostingRules.Object,
                AccountingPeriods.Object,
                JournalEntrySequences.Object,
                Accounts.Object,
                NullLogger<PostingEngine>.Instance
            );
    }

    [Fact]
    public async Task NC_de_compra_autorizada_genera_asiento_balanceado_Debe_CxP_Haber_gasto_e_IVA()
    {
        var m = new EngineMocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        var expense = PostableAccount("5.1.05", "Gasto/Inventario base");
        var vat = PostableAccount("1.1.07", "IVA Crédito Tributario");
        m.RegisterAccount(payable);
        m.RegisterAccount(expense);
        m.RegisterAccount(vat);
        var rule = Rule(payable.Id, expense.Id, vat.Id);
        m.PostingRules
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var creditNoteId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new PurchaseCreditNoteAuthorizedPostingTranslator(engine, NullLogger<PurchaseCreditNoteAuthorizedPostingTranslator>.Instance);

        await translator.Handle(AuthorizedEvent(creditNoteId, 100m, 15m, 115m), CancellationToken.None);

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.SourceModule.Should().Be("Purchases");
        entry.SourceEventType.Should().Be("PurchaseCreditNoteAuthorized");
        entry.SourceEventId.Should().Be(creditNoteId);
        entry.Lines.Should().HaveCount(3);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().Contain(l => l.AccountId == payable.Id && l.Debit == 115m && l.Credit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == expense.Id && l.Credit == 100m && l.Debit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == vat.Id && l.Credit == 15m && l.Debit == 0m);
    }

    [Fact]
    public async Task NC_de_compra_con_ICE_incluye_linea_ICE_y_el_asiento_sigue_balanceado()
    {
        var m = new EngineMocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        var expense = PostableAccount("5.1.05", "Gasto/Inventario base");
        var vat = PostableAccount("1.1.07", "IVA Crédito Tributario");
        var ice = PostableAccount("1.1.08", "ICE Crédito Tributario");
        m.RegisterAccount(payable);
        m.RegisterAccount(expense);
        m.RegisterAccount(vat);
        m.RegisterAccount(ice);
        var rule = RuleWithIce(payable.Id, expense.Id, vat.Id, ice.Id);
        m.PostingRules
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var creditNoteId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new PurchaseCreditNoteAuthorizedPostingTranslator(engine, NullLogger<PurchaseCreditNoteAuthorizedPostingTranslator>.Instance);

        // Subtotal 100 + VAT (100+10)*15%=16.5 + ICE 10 = 126.5 — mismo cálculo que
        // PurchaseCreditNoteTests.Authorize_con_ICE_propaga_IceAmount... — montos ya resueltos por
        // la entidad, nunca recalculados aquí.
        await translator.Handle(AuthorizedEvent(creditNoteId, subtotal: 100m, vat: 16.5m, appliedToPayable: 126.5m, iceAmount: 10m), CancellationToken.None);

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.Lines.Should().HaveCount(4);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().Contain(l => l.AccountId == payable.Id && l.Debit == 126.5m && l.Credit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == expense.Id && l.Credit == 100m && l.Debit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == vat.Id && l.Credit == 16.5m && l.Debit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == ice.Id && l.Credit == 10m && l.Debit == 0m);
    }

    [Fact]
    public async Task NC_de_compra_con_IRBPNR_incluye_linea_IRBPNR_y_el_asiento_sigue_balanceado()
    {
        var m = new EngineMocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        var expense = PostableAccount("5.1.05", "Gasto/Inventario base");
        var vat = PostableAccount("1.1.07", "IVA Crédito Tributario");
        var irbpnr = PostableAccount("1.1.09", "IRBPNR Crédito Tributario");
        m.RegisterAccount(payable);
        m.RegisterAccount(expense);
        m.RegisterAccount(vat);
        m.RegisterAccount(irbpnr);
        var rule = RuleWithIrbpnr(payable.Id, expense.Id, vat.Id, irbpnr.Id);
        m.PostingRules
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var creditNoteId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new PurchaseCreditNoteAuthorizedPostingTranslator(engine, NullLogger<PurchaseCreditNoteAuthorizedPostingTranslator>.Instance);

        await translator.Handle(
            AuthorizedEvent(creditNoteId, subtotal: 100m, vat: 15m, appliedToPayable: 121m, irbpnrAmount: 6m),
            CancellationToken.None
        );

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.Lines.Should().HaveCount(4);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().Contain(l => l.AccountId == payable.Id && l.Debit == 121m && l.Credit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == irbpnr.Id && l.Credit == 6m && l.Debit == 0m);
    }

    [Fact]
    public async Task NC_sin_IRBPNR_no_genera_linea_IRBPNR_falsa()
    {
        var m = new EngineMocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        var expense = PostableAccount("5.1.05", "Gasto/Inventario base");
        var vat = PostableAccount("1.1.07", "IVA Crédito Tributario");
        var irbpnr = PostableAccount("1.1.09", "IRBPNR Crédito Tributario");
        m.RegisterAccount(payable);
        m.RegisterAccount(expense);
        m.RegisterAccount(vat);
        m.RegisterAccount(irbpnr);
        var rule = RuleWithIrbpnr(payable.Id, expense.Id, vat.Id, irbpnr.Id);
        m.PostingRules
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var creditNoteId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new PurchaseCreditNoteAuthorizedPostingTranslator(engine, NullLogger<PurchaseCreditNoteAuthorizedPostingTranslator>.Instance);

        await translator.Handle(AuthorizedEvent(creditNoteId, 100m, 15m, 115m), CancellationToken.None);

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.Lines.Should().HaveCount(3, "la línea IRBPNR en monto cero nunca se contabiliza");
        entry.Lines.Should().NotContain(l => l.AccountId == irbpnr.Id);
    }

    [Fact]
    public async Task NC_sin_ICE_con_regla_que_tiene_linea_ICE_configurada_omite_esa_linea_sin_romper_el_balance()
    {
        // Misma PostingRule (con línea ICE) reutilizada para una NC SIN componente ICE
        // (IceAmount=0) — JournalFactory debe omitir la línea en cero automáticamente, sin
        // necesidad de lógica condicional en el translator.
        var m = new EngineMocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        var expense = PostableAccount("5.1.05", "Gasto/Inventario base");
        var vat = PostableAccount("1.1.07", "IVA Crédito Tributario");
        var ice = PostableAccount("1.1.08", "ICE Crédito Tributario");
        m.RegisterAccount(payable);
        m.RegisterAccount(expense);
        m.RegisterAccount(vat);
        m.RegisterAccount(ice);
        var rule = RuleWithIce(payable.Id, expense.Id, vat.Id, ice.Id);
        m.PostingRules
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var creditNoteId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new PurchaseCreditNoteAuthorizedPostingTranslator(engine, NullLogger<PurchaseCreditNoteAuthorizedPostingTranslator>.Instance);

        await translator.Handle(AuthorizedEvent(creditNoteId, 100m, 15m, 115m, iceAmount: 0m), CancellationToken.None);

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.Lines.Should().HaveCount(3, "la línea ICE en monto cero nunca se contabiliza");
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().NotContain(l => l.AccountId == ice.Id);
    }

    [Fact]
    public async Task NC_con_ICE_y_cuenta_ICE_inactiva_falla_con_POSTING_ACCOUNT_INVALID_y_no_persiste()
    {
        var m = new EngineMocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        var expense = PostableAccount("5.1.05", "Gasto/Inventario base");
        var vat = PostableAccount("1.1.07", "IVA Crédito Tributario");
        var ice = PostableAccount("1.1.08", "ICE Crédito Tributario");
        ice.Disable(CreatedBy);
        m.RegisterAccount(payable);
        m.RegisterAccount(expense);
        m.RegisterAccount(vat);
        m.RegisterAccount(ice);
        var rule = RuleWithIce(payable.Id, expense.Id, vat.Id, ice.Id);
        m.PostingRules
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var creditNoteId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new PurchaseCreditNoteAuthorizedPostingTranslator(engine, NullLogger<PurchaseCreditNoteAuthorizedPostingTranslator>.Instance);

        await translator.Handle(AuthorizedEvent(creditNoteId, subtotal: 100m, vat: 16.5m, appliedToPayable: 126.5m, iceAmount: 10m), CancellationToken.None);

        m.Captured.Should().BeNull();
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Evento_de_NC_duplicado_no_duplica_el_asiento()
    {
        var m = new EngineMocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        var expense = PostableAccount("5.1.05", "Gasto/Inventario base");
        var vat = PostableAccount("1.1.07", "IVA Crédito Tributario");
        m.RegisterAccount(payable);
        m.RegisterAccount(expense);
        m.RegisterAccount(vat);
        var rule = Rule(payable.Id, expense.Id, vat.Id);
        m.PostingRules
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var creditNoteId = Guid.NewGuid();
        var existing = JournalEntry.Create(
            TenantId, CompanyId, new DateOnly(2026, 8, 10), Guid.NewGuid(), 2026,
            "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, "NC ya contabilizada", CreatedBy
        );
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);

        var engine = m.BuildEngine();
        var translator = new PurchaseCreditNoteAuthorizedPostingTranslator(engine, NullLogger<PurchaseCreditNoteAuthorizedPostingTranslator>.Instance);

        await translator.Handle(AuthorizedEvent(creditNoteId), CancellationToken.None);

        m.Captured.Should().BeNull("un hecho ya contabilizado nunca debe generar un segundo asiento");
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cuenta_de_CxP_inactiva_falla_con_POSTING_ACCOUNT_INVALID_y_no_persiste()
    {
        var m = new EngineMocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        payable.Disable(CreatedBy);
        var expense = PostableAccount("5.1.05", "Gasto/Inventario base");
        var vat = PostableAccount("1.1.07", "IVA Crédito Tributario");
        m.RegisterAccount(payable);
        m.RegisterAccount(expense);
        m.RegisterAccount(vat);
        var rule = Rule(payable.Id, expense.Id, vat.Id);
        m.PostingRules
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var creditNoteId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new PurchaseCreditNoteAuthorizedPostingTranslator(engine, NullLogger<PurchaseCreditNoteAuthorizedPostingTranslator>.Instance);

        await translator.Handle(AuthorizedEvent(creditNoteId), CancellationToken.None);

        m.Captured.Should().BeNull();
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── PurchaseCreditNoteCancelledPostingTranslator ───────────────────────

    private static JournalEntry PostedCreditNoteEntry(Guid creditNoteId, int entryNumber = 1)
    {
        var entry = JournalEntry.Create(
            TenantId, CompanyId, new DateOnly(2026, 8, 10), Guid.NewGuid(), 2026,
            "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId, "Asiento de NC", CreatedBy
        );
        entry.AddLine(Guid.NewGuid(), null, 115m, 0m);
        entry.AddLine(Guid.NewGuid(), null, 0m, 115m);
        entry.Post(CreatedBy, entryNumber);
        return entry;
    }

    private sealed class TranslatorMocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<Microsoft.Extensions.Logging.ILogger<PurchaseCreditNoteCancelledPostingTranslator>> Logger { get; } = new();

        public PurchaseCreditNoteCancelledPostingTranslator BuildTranslator() =>
            new(JournalEntries.Object, Mediator.Object, Logger.Object);

        public void VerifyWarningLogged(Times times) =>
            Logger.Verify(
                l =>
                    l.Log(
                        Microsoft.Extensions.Logging.LogLevel.Warning,
                        It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                        It.IsAny<It.IsAnyType>(),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                    ),
                times
            );
    }

    [Fact]
    public async Task NC_con_asiento_Posted_envia_ReverseJournalEntryCommand_para_ese_asiento()
    {
        var creditNoteId = Guid.NewGuid();
        var original = PostedCreditNoteEntry(creditNoteId);
        var m = new TranslatorMocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Purchases", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { original });

        ReverseJournalEntryCommand? sent = null;
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto>>, CancellationToken>(
                (cmd, _) => sent = (ReverseJournalEntryCommand)cmd
            )
            .ReturnsAsync(
                Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto>.Success(
                    new ERP.Application.Modules.Accounting.DTOs.JournalEntryDto(
                        Guid.NewGuid(), original.EntryDate, original.AccountingPeriodId, original.FiscalYear,
                        "Accounting", "Reversal", original.Id, "Reverso", "Posted", 2, DateTime.UtcNow,
                        original.Id, null, null, null
                    )
                )
            );

        var translator = m.BuildTranslator();
        await translator.Handle(CancelledEvent(creditNoteId, 115m), CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.JournalEntryId.Should().Be(original.Id);
        sent.Reason.Should().Contain("NC-001-001-000000005").And.Contain("Error de digitación");
        m.VerifyWarningLogged(Times.Never());
    }

    [Fact]
    public async Task NC_cancelada_desde_Draft_AppliedToPayableAmount_null_no_envia_comando()
    {
        var creditNoteId = Guid.NewGuid();
        var m = new TranslatorMocks();

        var translator = m.BuildTranslator();
        await translator.Handle(CancelledEvent(creditNoteId, appliedToPayable: null), CancellationToken.None);

        m.Mediator.Verify(
            x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.JournalEntries.Verify(
            r => r.GetBySourceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "sin AppliedToPayableAmount no hay nada que buscar/reversar"
        );
    }

    [Fact]
    public async Task Sin_asiento_Posted_no_envia_comando_ni_genera_warning()
    {
        var creditNoteId = Guid.NewGuid();
        var m = new TranslatorMocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Purchases", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry>());

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(CancelledEvent(creditNoteId, 115m), CancellationToken.None);

        await act.Should().NotThrowAsync();
        m.Mediator.Verify(
            x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.VerifyWarningLogged(Times.Never());
    }

    [Fact]
    public async Task Reverso_fallido_genera_warning_y_no_lanza_excepcion()
    {
        var creditNoteId = Guid.NewGuid();
        var original = PostedCreditNoteEntry(creditNoteId);
        var m = new TranslatorMocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Purchases", creditNoteId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { original });
        m.Mediator
            .Setup(x => x.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<ERP.Application.Modules.Accounting.DTOs.JournalEntryDto>.ValidationFailure(
                    "El período contable está cerrado.",
                    "PERIOD_NOT_OPEN"
                )
            );

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(CancelledEvent(creditNoteId, 115m), CancellationToken.None);

        await act.Should().NotThrowAsync();
        m.VerifyWarningLogged(Times.Once());
    }
}
