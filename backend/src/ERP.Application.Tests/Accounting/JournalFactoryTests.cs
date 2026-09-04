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
        decimal grandTotal = 115m,
        IReadOnlyCollection<PostingAllocation>? allocations = null
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
            grandTotal,
            Allocations: allocations
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

    // ACCOUNTING-POSTING-RULES-AUDIT-03: PostingAccountGuard solo usa el Id pasado a
    // GetByIdAsync como clave de búsqueda — nunca compara account.Id contra ese parámetro — así
    // que una única cuenta "siempre postable" alcanza para las pruebas de esta suite (el foco es
    // JournalFactory, no la validación de cuentas, que tiene su propia suite dedicada).
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

    // ── EXPENSES-POSTING-ALLOCATIONS-06: líneas dinámicas por cuenta (PostingFact.Allocations) ──

    [Fact]
    public async Task Fact_con_una_allocation_genera_una_JournalEntryLine_adicional()
    {
        var allocationAccount = Guid.NewGuid();
        var creditAccount = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(creditAccount, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var allocations = new[]
        {
            new PostingAllocation(allocationAccount, 115m, AccountNature.Debit, "Gasto de oficina"),
        };

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact(allocations: allocations));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().HaveCount(2);
        m.Captured.Lines.Should()
            .Contain(l =>
                l.AccountId == allocationAccount
                && l.Debit == 115m
                && l.Credit == 0m
                && l.Description == "Gasto de oficina"
            );
    }

    [Fact]
    public async Task Fact_con_tres_allocations_genera_tres_JournalEntryLines_una_por_cuenta()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var accountC = Guid.NewGuid();
        var creditAccount = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(creditAccount, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var allocations = new[]
        {
            new PostingAllocation(accountA, 50m, AccountNature.Debit),
            new PostingAllocation(accountB, 40m, AccountNature.Debit),
            new PostingAllocation(accountC, 25m, AccountNature.Debit),
        };

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact(grandTotal: 115m, allocations: allocations));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().HaveCount(4); // 1 crédito fijo + 3 allocations
        m.Captured.Lines.Should().Contain(l => l.AccountId == accountA && l.Debit == 50m);
        m.Captured.Lines.Should().Contain(l => l.AccountId == accountB && l.Debit == 40m);
        m.Captured.Lines.Should().Contain(l => l.AccountId == accountC && l.Debit == 25m);
    }

    [Fact]
    public async Task Asiento_con_allocations_queda_balanceado_Debe_igual_a_Haber()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var accountC = Guid.NewGuid();
        var creditAccount = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(creditAccount, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var allocations = new[]
        {
            new PostingAllocation(accountA, 50m, AccountNature.Debit),
            new PostingAllocation(accountB, 40m, AccountNature.Debit),
            new PostingAllocation(accountC, 25m, AccountNature.Debit),
        };

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact(grandTotal: 115m, allocations: allocations));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Sum(l => l.Debit).Should().Be(m.Captured.Lines.Sum(l => l.Credit));
        // Post() invoca EnsureBalanced() internamente — un asiento desbalanceado nunca llega a
        // Posted, así que este estado por sí solo ya prueba el balance.
        m.Captured.Status.Should().Be(JournalEntryStatus.Posted);
    }

    // ── RETENTIONS-EXPENSES-INTEGRATION-01D-2: PostingAmountKind.Retention (antes resolvía 0m
    // siempre — ver comentario histórico en JournalFactory.ResolveAmount) ─────────────────────

    private static PostingFact RetentionFact(decimal retainedAmount = 4.50m) =>
        new(
            TenantId,
            CompanyId,
            "Retentions",
            "DocumentIssued",
            Guid.NewGuid(),
            new DateOnly(2026, 9, 3),
            Subtotal: 0m,
            TotalVat: retainedAmount,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: retainedAmount,
            RetainedAmount: retainedAmount
        );

    [Fact]
    public async Task PostingAmountKind_Retention_resuelve_PostingFact_RetainedAmount()
    {
        var accountId = Guid.NewGuid();
        var rule = PostingRule.Create(TenantId, CompanyId, "Retentions", "DocumentIssued", null, null, null, CreatedBy);
        rule.AddLine(accountId, AccountNature.Debit, PostingAmountKind.Retention);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.Retention);

        var m = new Mocks();
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Retentions", "DocumentIssued", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var result = await m.BuildEngine().PostAsync(RetentionFact(4.50m));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().Contain(l => l.AccountId == accountId && l.Debit == 4.50m);
    }

    /// <summary>
    /// Reproduce el ejemplo conceptual del ticket (docs/decisions/RETENTIONS-MODULE-DESIGN-01.md §
    /// "Impacto contable"): Gasto 100 + IVA 15 = 115; Retención IVA 4.50 → el asiento de la
    /// RETENCIÓN (separado del asiento del gasto, que no cambia) es Debe CxP proveedor 4.50, Haber
    /// Retención IVA por pagar 4.50 — reclasificación, nunca modifica el asiento del gasto ya
    /// generado.
    /// </summary>
    [Fact]
    public async Task Asiento_de_retencion_acredita_Retencion_por_pagar_y_debita_CxP_proveedor_balanceado()
    {
        var accountsPayableAccount = Guid.NewGuid();
        var retentionPayableAccount = Guid.NewGuid();
        var rule = PostingRule.Create(TenantId, CompanyId, "Retentions", "DocumentIssued", null, null, null, CreatedBy);
        rule.AddLine(accountsPayableAccount, AccountNature.Debit, PostingAmountKind.Retention);
        rule.AddLine(retentionPayableAccount, AccountNature.Credit, PostingAmountKind.Retention);

        var m = new Mocks();
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Retentions", "DocumentIssued", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var result = await m.BuildEngine().PostAsync(RetentionFact(4.50m));

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().HaveCount(2);
        m.Captured.Lines.Should().Contain(l => l.AccountId == accountsPayableAccount && l.Debit == 4.50m && l.Credit == 0m);
        m.Captured.Lines.Should().Contain(l => l.AccountId == retentionPayableAccount && l.Credit == 4.50m && l.Debit == 0m);
        m.Captured.Lines.Sum(l => l.Debit).Should().Be(m.Captured.Lines.Sum(l => l.Credit));
        // Post() invoca EnsureBalanced() internamente — un asiento desbalanceado nunca llega a Posted.
        m.Captured.Status.Should().Be(JournalEntryStatus.Posted);
    }

    [Fact]
    public async Task Sin_PostingRule_configurada_para_Retentions_el_posting_falla_fail_closed()
    {
        var m = new Mocks();
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Retentions", "DocumentIssued", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostingRule?)null);

        var result = await m.BuildEngine().PostAsync(RetentionFact());

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("RULE_NOT_FOUND");
    }
}
