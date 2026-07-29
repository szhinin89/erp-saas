using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// JournalFactory/JournalValidator son <c>internal</c> (sin InternalsVisibleTo hacia este
/// ensamblado — no hay precedente de ese patrón en el proyecto) — se ejercitan indirectamente a
/// través de <see cref="PostingEngine.PostAsync"/> con repositorios mockeados, mismo criterio ya
/// usado en <see cref="PostingEngineTests"/>. Esta suite se enfoca en el comportamiento de
/// JournalFactory: construcción de líneas a partir de PostingRuleLine (Fase 3.5.5).
/// </summary>
public sealed class JournalFactoryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static PostingFact Fact(
        decimal subtotal = 100m,
        decimal totalVat = 15m,
        decimal totalIce = 0m,
        decimal totalDiscount = 0m,
        decimal grandTotal = 115m
    ) =>
        new(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            new DateOnly(2026, 7, 15),
            subtotal,
            totalVat,
            totalIce,
            totalDiscount,
            grandTotal
        );

    private static AccountingPeriod OpenPeriod() =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            2026,
            7,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            CreatedBy
        );

    private static PostingRule EmptyRule() =>
        PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            null,
            null,
            null,
            CreatedBy
        );

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<IAccountingPeriodRepository> AccountingPeriods { get; } = new();
        public Mock<IJournalEntrySequenceRepository> JournalEntrySequences { get; } = new();
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
                JournalEntrySequences.Object
            );
    }

    [Fact]
    public async Task Regla_con_una_linea_debito_genera_JournalEntryLine_con_Debit()
    {
        var accountId = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(accountId, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.Subtotal); // balance

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact());

        result.IsSuccess.Should().BeTrue();
        m.Captured.Should().NotBeNull();
        m.Captured!.Lines.Should()
            .Contain(l => l.AccountId == accountId && l.Debit == 100m && l.Credit == 0m);
    }

    [Fact]
    public async Task Regla_con_una_linea_credito_genera_JournalEntryLine_con_Credit()
    {
        var accountId = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(Guid.NewGuid(), AccountNature.Debit, PostingAmountKind.GrandTotal); // balance
        rule.AddLine(accountId, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact());

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should()
            .Contain(l => l.AccountId == accountId && l.Credit == 115m && l.Debit == 0m);
    }

    [Fact]
    public async Task Regla_con_multiples_lineas_genera_una_JournalEntryLine_por_cada_mapeo()
    {
        var debitAccount = Guid.NewGuid();
        var creditSubtotalAccount = Guid.NewGuid();
        var creditVatAccount = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(debitAccount, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(creditSubtotalAccount, AccountNature.Credit, PostingAmountKind.Subtotal);
        rule.AddLine(creditVatAccount, AccountNature.Credit, PostingAmountKind.TaxVat);

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact());

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().HaveCount(3);
        m.Captured.Lines.Should().Contain(l => l.AccountId == debitAccount && l.Debit == 115m);
        m.Captured.Lines.Should()
            .Contain(l => l.AccountId == creditSubtotalAccount && l.Credit == 100m);
        m.Captured.Lines.Should().Contain(l => l.AccountId == creditVatAccount && l.Credit == 15m);
    }

    [Theory]
    [InlineData(PostingAmountKind.Subtotal, 100)]
    [InlineData(PostingAmountKind.TaxVat, 15)]
    [InlineData(PostingAmountKind.GrandTotal, 115)]
    public async Task Regla_resuelve_el_monto_correcto_segun_PostingAmountKind(
        PostingAmountKind kind,
        decimal expectedAmount
    )
    {
        var accountId = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(accountId, AccountNature.Debit, kind);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, kind); // misma cantidad, cuenta distinta, para balancear

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact());

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should()
            .Contain(l => l.AccountId == accountId && l.Debit == expectedAmount);
    }

    [Fact]
    public async Task Regla_omite_lineas_cuyo_monto_resuelto_es_cero()
    {
        var debitAccount = Guid.NewGuid();
        var iceAccount = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(debitAccount, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(iceAccount, AccountNature.Credit, PostingAmountKind.TaxIce); // TotalIce = 0 en Fact()
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.Subtotal); // balance real

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact(totalIce: 0m));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should()
            .NotContain(
                l => l.AccountId == iceAccount,
                because: "la línea de ICE se omite porque el monto resuelto es cero — nunca se contabiliza en cero"
            );
    }
}
