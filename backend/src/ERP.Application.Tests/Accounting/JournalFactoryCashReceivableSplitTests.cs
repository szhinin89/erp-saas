using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 — confirma que
/// <c>JournalFactory</c> (vía <see cref="PostingEngine.PostAsync"/>, mismo criterio de acceso
/// indirecto que <see cref="JournalFactoryTests"/>) resuelve los dos <see cref="PostingAmountKind"/>
/// nuevos (<c>CashApplied</c>/<c>PendingBalance</c>) y que la línea cuyo monto resuelve en cero se
/// omite automáticamente (mecanismo ya existente, reutilizado — nunca se contabiliza una CxC
/// ficticia en una venta 100% contado, ni una línea de Caja en una venta 100% crédito).
/// </summary>
public sealed class JournalFactoryCashReceivableSplitTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static PostingFact Fact(decimal cashApplied, decimal pendingBalance) =>
        new(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            new DateOnly(2026, 9, 13),
            Subtotal: 100m,
            TotalVat: 15m,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: cashApplied + pendingBalance,
            CashApplied: cashApplied,
            PendingBalance: pendingBalance
        );

    private static AccountingPeriod OpenPeriod() =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            2026,
            9,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            CreatedBy
        );

    private static Account PostableAccount() =>
        Account.Create(
            TenantId,
            CompanyId,
            ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.01"),
            "Cuenta de prueba",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            CreatedBy
        );

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<IAccountingPeriodRepository> AccountingPeriods { get; } = new();
        public Mock<IJournalEntrySequenceRepository> JournalEntrySequences { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public JournalEntry? Captured { get; private set; }

        public Mocks()
        {
            Accounts
                .Setup(r =>
                    r.GetByIdAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(PostableAccount());
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
                .Setup(r =>
                    r.FindByKeyAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((JournalEntry?)null);
            JournalEntries
                .Setup(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
                .Callback<JournalEntry, CancellationToken>((entry, _) => Captured = entry)
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

        public void SetupRule(PostingRule rule) =>
            PostingRules
                .Setup(r =>
                    r.FindByKeyAsync(
                        TenantId,
                        CompanyId,
                        "Sales",
                        "InvoiceIssued",
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(rule);

        public PostingEngine BuildEngine() =>
            new(
                JournalEntries.Object,
                PostingRules.Object,
                AccountingPeriods.Object,
                JournalEntrySequences.Object,
                Accounts.Object,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PostingEngine>.Instance
            );
    }

    // Mismas 5 líneas que AccountingBootstrapStep.MinimalPostingRules sembra hoy para
    // "Sales"/"InvoiceIssued" (Lote 3) — Debe Caja(CashApplied) + Debe CxC(PendingBalance),
    // Haber Ventas(Subtotal) + Haber IVA(TaxVat) + Haber ICE(TaxIce).
    private static (PostingRule rule, Guid cash, Guid receivable, Guid sales, Guid vat, Guid ice) SplitRule()
    {
        var rule = PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            null,
            null,
            null,
            CreatedBy
        );
        var cash = Guid.NewGuid();
        var receivable = Guid.NewGuid();
        var sales = Guid.NewGuid();
        var vat = Guid.NewGuid();
        var ice = Guid.NewGuid();
        rule.AddLine(cash, AccountNature.Debit, PostingAmountKind.CashApplied);
        rule.AddLine(receivable, AccountNature.Debit, PostingAmountKind.PendingBalance);
        rule.AddLine(sales, AccountNature.Credit, PostingAmountKind.Subtotal);
        rule.AddLine(vat, AccountNature.Credit, PostingAmountKind.TaxVat);
        rule.AddLine(ice, AccountNature.Credit, PostingAmountKind.TaxIce);
        return (rule, cash, receivable, sales, vat, ice);
    }

    [Fact]
    public async Task Venta_contado_omite_la_linea_de_CxC_ninguna_CxC_ficticia()
    {
        var (rule, cash, receivable, _, _, _) = SplitRule();
        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact(cashApplied: 115m, pendingBalance: 0m));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().Contain(l => l.AccountId == cash && l.Debit == 115m);
        m.Captured.Lines.Should()
            .NotContain(l => l.AccountId == receivable, "PendingBalance=0 nunca genera línea de CxC");
    }

    [Fact]
    public async Task Venta_credito_puro_omite_la_linea_de_Caja_sin_movimiento_de_caja_ficticio()
    {
        var (rule, cash, receivable, _, _, _) = SplitRule();
        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact(cashApplied: 0m, pendingBalance: 115m));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().Contain(l => l.AccountId == receivable && l.Debit == 115m);
        m.Captured.Lines.Should()
            .NotContain(l => l.AccountId == cash, "CashApplied=0 nunca genera línea de Caja");
    }

    [Fact]
    public async Task Venta_parcial_genera_ambas_lineas_Caja_y_CxC_balanceadas()
    {
        var (rule, cash, receivable, sales, vat, _) = SplitRule();
        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact(cashApplied: 40m, pendingBalance: 75m));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().Contain(l => l.AccountId == cash && l.Debit == 40m);
        m.Captured.Lines.Should().Contain(l => l.AccountId == receivable && l.Debit == 75m);
        m.Captured.Lines.Should().Contain(l => l.AccountId == sales && l.Credit == 100m);
        m.Captured.Lines.Should().Contain(l => l.AccountId == vat && l.Credit == 15m);

        var totalDebit = m.Captured.Lines.Sum(l => l.Debit);
        var totalCredit = m.Captured.Lines.Sum(l => l.Credit);
        totalDebit.Should().Be(totalCredit).And.Be(115m);
    }
}
