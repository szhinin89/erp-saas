using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CASH-POSTING-06 — cobro de cliente y pago a proveedor a través del pipeline REAL
/// (CollectionAppliedEvent/SupplierPaymentAppliedEvent → Translator → PostingEngine real, con
/// repositorios mockeados) — mismo patrón que <see cref="SalesReturnPostingPipelineTests"/>.
/// Confirma que las reglas ya endurecidas en ACCOUNTING-POSTING-RULES-AUDIT-03
/// (PostingAccountGuard/idempotencia) aplican igual para el módulo Finance, sin necesitar código
/// nuevo en el Posting Engine — el pipeline es genérico por diseño.
/// </summary>
public sealed class CollectionAndSupplierPaymentPostingPipelineTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid PartnerId = Guid.NewGuid();

    private static CollectionAppliedEvent CollectionEvent(
        Guid paymentId,
        decimal amount = 300m,
        Guid? financialDestinationId = null
    ) =>
        new(
            TenantId,
            paymentId,
            CompanyId,
            PartnerId,
            amount,
            new DateOnly(2026, 8, 10),
            financialDestinationId
        );

    private static SupplierPaymentAppliedEvent SupplierPaymentEvent(
        Guid paymentId,
        decimal amount = 300m,
        Guid? financialDestinationId = null
    ) =>
        new(
            TenantId,
            paymentId,
            CompanyId,
            PartnerId,
            amount,
            new DateOnly(2026, 8, 10),
            financialDestinationId
        );

    private static CompanyFinancialDestination BankDestination(Guid accountingAccountId) =>
        CompanyFinancialDestination.Create(
            TenantId,
            CompanyId,
            "BANCO-01",
            "Banco Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            accountingAccountId,
            "USD",
            CreatedBy,
            bankInstitutionCode: "PICHINCHA",
            bankAccountIdentifierNormalized: "1234567890"
        );

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

    private static PostingRule Rule(string sourceModule, string factType, Guid debitAccountId, Guid creditAccountId)
    {
        var rule = PostingRule.Create(TenantId, CompanyId, sourceModule, factType, null, null, null, CreatedBy);
        rule.AddLine(debitAccountId, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(creditAccountId, AccountNature.Credit, PostingAmountKind.GrandTotal);
        return rule;
    }

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<IAccountingPeriodRepository> AccountingPeriods { get; } = new();
        public Mock<IJournalEntrySequenceRepository> JournalEntrySequences { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<ICompanyFinancialDestinationRepository> FinancialDestinations { get; } = new();
        public JournalEntry? Captured { get; private set; }

        public Mocks()
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

        public void RegisterFinancialDestination(CompanyFinancialDestination destination) =>
            FinancialDestinations
                .Setup(r =>
                    r.GetByIdAsync(TenantId, destination.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(destination);

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
    public async Task Cobro_de_cliente_genera_asiento_balanceado_Debe_Caja_Haber_CxC()
    {
        var m = new Mocks();
        var cash = PostableAccount("1.1.01", "Caja");
        var receivable = PostableAccount("1.1.05", "Cuentas por Cobrar");
        m.RegisterAccount(cash);
        m.RegisterAccount(receivable);
        var rule = Rule("Finance", "CollectionApplied", cash.Id, receivable.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new CollectionAppliedPostingTranslator(engine, m.FinancialDestinations.Object, NullLogger<CollectionAppliedPostingTranslator>.Instance);

        var paymentId = Guid.NewGuid();
        await translator.Handle(CollectionEvent(paymentId, 300m), CancellationToken.None);

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.Lines.Should().HaveCount(2);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().Contain(l => l.AccountId == cash.Id && l.Debit == 300m && l.Credit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == receivable.Id && l.Credit == 300m && l.Debit == 0m);
        entry.SourceModule.Should().Be("Finance");
        entry.SourceEventType.Should().Be("CollectionApplied");
        entry.SourceEventId.Should().Be(paymentId);
    }

    [Fact]
    public async Task Pago_a_proveedor_genera_asiento_balanceado_Debe_CxP_Haber_Caja()
    {
        var m = new Mocks();
        var payable = PostableAccount("2.1.01", "Cuentas por Pagar");
        var cash = PostableAccount("1.1.01", "Caja");
        m.RegisterAccount(payable);
        m.RegisterAccount(cash);
        // Debe: CxP (se cancela la deuda) — Haber: Caja (sale el efectivo).
        var rule = Rule("Finance", "SupplierPaymentApplied", payable.Id, cash.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "SupplierPaymentApplied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "SupplierPaymentApplied", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new SupplierPaymentAppliedPostingTranslator(engine, m.FinancialDestinations.Object, NullLogger<SupplierPaymentAppliedPostingTranslator>.Instance);

        var paymentId = Guid.NewGuid();
        await translator.Handle(SupplierPaymentEvent(paymentId, 300m), CancellationToken.None);

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.Lines.Should().HaveCount(2);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().Contain(l => l.AccountId == payable.Id && l.Debit == 300m && l.Credit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == cash.Id && l.Credit == 300m && l.Debit == 0m);
    }

    [Fact]
    public async Task Evento_de_cobro_duplicado_no_duplica_el_asiento()
    {
        var m = new Mocks();
        var cash = PostableAccount("1.1.01", "Caja");
        var receivable = PostableAccount("1.1.05", "Cuentas por Cobrar");
        m.RegisterAccount(cash);
        m.RegisterAccount(receivable);
        var rule = Rule("Finance", "CollectionApplied", cash.Id, receivable.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var paymentId = Guid.NewGuid();
        var existing = JournalEntry.Create(
            TenantId,
            CompanyId,
            new DateOnly(2026, 8, 10),
            Guid.NewGuid(),
            2026,
            "Finance",
            "CollectionApplied",
            paymentId,
            "Cobro ya contabilizado",
            CreatedBy
        );
        // La clave de idempotencia es (CompanyId, SourceModule, SourceEventId, FactType) — el
        // reprocesamiento del mismo evento debe encontrar este JournalEntry ya existente y
        // devolver AlreadyProcessed sin generar uno nuevo (PostingIdempotencyGuard).
        m.JournalEntries
            .Setup(r =>
                r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", paymentId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);

        var engine = m.BuildEngine();
        var translator = new CollectionAppliedPostingTranslator(engine, m.FinancialDestinations.Object, NullLogger<CollectionAppliedPostingTranslator>.Instance);

        await translator.Handle(CollectionEvent(paymentId, 300m), CancellationToken.None);

        m.Captured.Should().BeNull("un hecho ya contabilizado nunca debe generar un segundo JournalEntry");
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Cuenta_inactiva_en_la_regla_de_cobro_falla_con_POSTING_ACCOUNT_INVALID_y_no_persiste()
    {
        var m = new Mocks();
        var cash = PostableAccount("1.1.01", "Caja");
        cash.Disable(CreatedBy); // PostingAccountGuard (ACCOUNTING-POSTING-RULES-AUDIT-03) aplica igual para Finance.
        var receivable = PostableAccount("1.1.05", "Cuentas por Cobrar");
        m.RegisterAccount(cash);
        m.RegisterAccount(receivable);
        var rule = Rule("Finance", "CollectionApplied", cash.Id, receivable.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new CollectionAppliedPostingTranslator(engine, m.FinancialDestinations.Object, NullLogger<CollectionAppliedPostingTranslator>.Instance);

        await translator.Handle(CollectionEvent(Guid.NewGuid(), 300m), CancellationToken.None);

        m.Captured.Should().BeNull("una cuenta inválida nunca debe producir un asiento, ni siquiera parcial");
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 ───────────────────────

    [Fact]
    public async Task Cobro_con_destino_financiero_transferencia_postea_a_Banco_en_vez_de_Caja()
    {
        var m = new Mocks();
        var cashDefault = PostableAccount("1.1.01", "Caja");
        var bank = PostableAccount("1.1.02", "Banco");
        var receivable = PostableAccount("1.1.05", "Cuentas por Cobrar");
        m.RegisterAccount(cashDefault);
        m.RegisterAccount(bank);
        m.RegisterAccount(receivable);
        var destination = BankDestination(bank.Id);
        m.RegisterFinancialDestination(destination);
        // La PostingRule sigue apuntando a Caja por defecto — el override debe ganar.
        var rule = Rule("Finance", "CollectionApplied", cashDefault.Id, receivable.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new CollectionAppliedPostingTranslator(engine, m.FinancialDestinations.Object, NullLogger<CollectionAppliedPostingTranslator>.Instance);

        var paymentId = Guid.NewGuid();
        await translator.Handle(
            CollectionEvent(paymentId, 300m, destination.Id),
            CancellationToken.None
        );

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().Contain(l => l.AccountId == bank.Id && l.Debit == 300m && l.Credit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == receivable.Id && l.Credit == 300m && l.Debit == 0m);
        entry.Lines.Should().NotContain(l => l.AccountId == cashDefault.Id);
    }

    [Fact]
    public async Task Cobro_con_destino_financiero_cuya_cuenta_ya_no_es_postable_bloquea_solo_el_posting()
    {
        var m = new Mocks();
        var cashDefault = PostableAccount("1.1.01", "Caja");
        var bank = PostableAccount("1.1.02", "Banco");
        bank.Disable(CreatedBy); // La cuenta se desactivó DESPUÉS de configurar el destino financiero.
        var receivable = PostableAccount("1.1.05", "Cuentas por Cobrar");
        m.RegisterAccount(cashDefault);
        m.RegisterAccount(bank);
        m.RegisterAccount(receivable);
        var destination = BankDestination(bank.Id);
        m.RegisterFinancialDestination(destination);
        var rule = Rule("Finance", "CollectionApplied", cashDefault.Id, receivable.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Finance", "CollectionApplied", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);

        var engine = m.BuildEngine();
        var translator = new CollectionAppliedPostingTranslator(engine, m.FinancialDestinations.Object, NullLogger<CollectionAppliedPostingTranslator>.Instance);

        var act = async () =>
            await translator.Handle(
                CollectionEvent(Guid.NewGuid(), 300m, destination.Id),
                CancellationToken.None
            );

        await act.Should().NotThrowAsync("el cobro ya aplicado nunca debe verse afectado por un fallo de posting");
        m.Captured.Should().BeNull("la cuenta efectiva (override) es inválida — ningún asiento parcial");
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
