using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// P0-02 Fase 6 — Remediación 01: prueba que los 5 <see cref="PostingAmountKind"/> nuevos
/// (<c>AppliedToPayable</c>/<c>SupplierCredit</c>/<c>CostVarianceDebit</c>/<c>CostVarianceCredit</c>/
/// <c>HistoricalCost</c>) resuelven correctamente los campos nuevos de <see cref="PostingFact"/>,
/// que los 6 kinds preexistentes no cambiaron de comportamiento, y que un asiento compuesto de 7
/// valores independientes (§19.1bis del diseño) balancea en los 3 casos de variación de costo
/// (positiva/negativa/cero) — ejercitado a través del pipeline real (<c>PostingEngine.PostAsync</c>
/// con repositorios mockeados), mismo criterio que <see cref="JournalFactoryTests"/>.
/// </summary>
public sealed class PurchaseReturnPostingAmountKindRemediationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static PostingFact ExistingShapeFact(
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

    private static PostingFact PurchaseReturnFact(
        decimal appliedToPayable,
        decimal supplierCredit,
        decimal costVarianceDebit,
        decimal costVarianceCredit,
        decimal historicalCost,
        decimal returnedVat,
        decimal returnedIce
    ) =>
        new(
            TenantId,
            CompanyId,
            "Purchases",
            "PurchaseReturn",
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            Subtotal: 0m,
            TotalVat: returnedVat,
            TotalIce: returnedIce,
            TotalDiscount: 0m,
            GrandTotal: 0m,
            AppliedToPayableAmount: appliedToPayable,
            SupplierCreditAmount: supplierCredit,
            CostVarianceDebitAmount: costVarianceDebit,
            CostVarianceCreditAmount: costVarianceCredit,
            HistoricalCostTotal: historicalCost
        );

    private static AccountingPeriod OpenPeriod(int year, int month) =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            year,
            month,
            new DateOnly(year, month, 1),
            new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            CreatedBy
        );

    private static PostingRule EmptyRule(string sourceModule, string factType) =>
        PostingRule.Create(
            TenantId,
            CompanyId,
            sourceModule,
            factType,
            null,
            null,
            null,
            CreatedBy
        );

    // ACCOUNTING-POSTING-RULES-AUDIT-03: PostingAccountGuard solo usa el Id pasado a
    // GetByIdAsync como clave de búsqueda — nunca compara account.Id contra ese parámetro — así
    // que una única cuenta "siempre postable" alcanza para las pruebas de esta suite (el foco es
    // PostingAmountKind, no la validación de cuentas, que tiene su propia suite dedicada).
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

        public Mocks(int year = 2026, int month = 7)
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
                .ReturnsAsync(OpenPeriod(year, month));
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

        public void SetupRule(string sourceModule, string factType, PostingRule rule) =>
            PostingRules
                .Setup(r =>
                    r.FindByKeyAsync(
                        TenantId,
                        CompanyId,
                        sourceModule,
                        factType,
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

    // ── 1. Los AmountKind existentes siguen resolviendo igual (regresión) ──────

    [Theory]
    [InlineData(PostingAmountKind.Subtotal, 100)]
    [InlineData(PostingAmountKind.TaxVat, 15)]
    [InlineData(PostingAmountKind.TaxIce, 0)]
    [InlineData(PostingAmountKind.Discount, 0)]
    [InlineData(PostingAmountKind.GrandTotal, 115)]
    public async Task Los_6_AmountKind_preexistentes_resuelven_exactamente_igual_que_antes(
        PostingAmountKind kind,
        decimal expectedAmount
    )
    {
        var accountId = Guid.NewGuid();
        var rule = EmptyRule("Sales", "InvoiceIssued");
        rule.AddLine(accountId, AccountNature.Debit, kind);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, kind);
        // Línea de balance siempre no-nula, independiente del kind bajo prueba — evita un asiento
        // totalmente vacío cuando el kind probado resuelve a 0 (TaxIce/Discount en ExistingShapeFact()).
        rule.AddLine(Guid.NewGuid(), AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.GrandTotal);

        var m = new Mocks();
        m.SetupRule("Sales", "InvoiceIssued", rule);

        var result = await m.BuildEngine().PostAsync(ExistingShapeFact());

        result.IsSuccess.Should().BeTrue();
        if (expectedAmount == 0)
            m.Captured!.Lines.Should().NotContain(l => l.AccountId == accountId);
        else
            m.Captured!.Lines.Should()
                .Contain(l => l.AccountId == accountId && l.Debit == expectedAmount);
    }

    // ── 2/3. Los 5 AmountKind nuevos resuelven los campos nuevos, nunca 0 cuando el campo tiene valor ──

    [Theory]
    [InlineData(PostingAmountKind.AppliedToPayable, 300)]
    [InlineData(PostingAmountKind.SupplierCredit, 50)]
    [InlineData(PostingAmountKind.CostVarianceDebit, 45)]
    [InlineData(PostingAmountKind.HistoricalCost, 345)]
    public async Task Los_AmountKind_nuevos_resuelven_el_campo_correcto_de_PostingFact_sin_devolver_0(
        PostingAmountKind kind,
        decimal expectedAmount
    )
    {
        var accountId = Guid.NewGuid();
        var rule = EmptyRule("Purchases", "PurchaseReturn");
        rule.AddLine(accountId, AccountNature.Debit, kind);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, kind);

        var m = new Mocks(2026, 8);
        m.SetupRule("Purchases", "PurchaseReturn", rule);

        var fact = PurchaseReturnFact(
            appliedToPayable: 300m,
            supplierCredit: 50m,
            costVarianceDebit: 45m,
            costVarianceCredit: 0m,
            historicalCost: 345m,
            returnedVat: 36m,
            returnedIce: 0m
        );

        var result = await m.BuildEngine().PostAsync(fact);

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should()
            .Contain(l => l.AccountId == accountId && l.Debit == expectedAmount);
    }

    [Fact]
    public async Task CostVarianceCredit_resuelve_el_campo_CostVarianceCreditAmount()
    {
        var accountId = Guid.NewGuid();
        var rule = EmptyRule("Purchases", "PurchaseReturn");
        rule.AddLine(Guid.NewGuid(), AccountNature.Debit, PostingAmountKind.CostVarianceCredit); // balance
        rule.AddLine(accountId, AccountNature.Credit, PostingAmountKind.CostVarianceCredit);

        var m = new Mocks(2026, 8);
        m.SetupRule("Purchases", "PurchaseReturn", rule);

        var fact = PurchaseReturnFact(
            appliedToPayable: 0m,
            supplierCredit: 0m,
            costVarianceDebit: 0m,
            costVarianceCredit: 20m,
            historicalCost: 0m,
            returnedVat: 0m,
            returnedIce: 0m
        );

        var result = await m.BuildEngine().PostAsync(fact);

        result.IsSuccess.Should().BeTrue();
        m.Captured!.Lines.Should().Contain(l => l.AccountId == accountId && l.Credit == 20m);
    }

    // ── 4. Un PostingFact de PurchaseReturnAuthorized puede transportar los 7 valores ────

    [Fact]
    public void PostingFact_puede_construirse_con_los_7_valores_de_PurchaseReturnAuthorized()
    {
        var fact = PurchaseReturnFact(
            appliedToPayable: 300m,
            supplierCredit: 50m,
            costVarianceDebit: 45m,
            costVarianceCredit: 0m,
            historicalCost: 345m,
            returnedVat: 36m,
            returnedIce: 5m
        );

        fact.AppliedToPayableAmount.Should().Be(300m);
        fact.SupplierCreditAmount.Should().Be(50m);
        fact.CostVarianceDebitAmount.Should().Be(45m);
        fact.CostVarianceCreditAmount.Should().Be(0m);
        fact.HistoricalCostTotal.Should().Be(345m);
        fact.TotalVat.Should().Be(36m);
        fact.TotalIce.Should().Be(5m);
    }

    [Fact]
    public void Los_campos_nuevos_de_PostingFact_son_null_por_defecto_para_call_sites_existentes()
    {
        var fact = ExistingShapeFact();

        fact.AppliedToPayableAmount.Should().BeNull();
        fact.SupplierCreditAmount.Should().BeNull();
        fact.CostVarianceDebitAmount.Should().BeNull();
        fact.CostVarianceCreditAmount.Should().BeNull();
        fact.HistoricalCostTotal.Should().BeNull();
    }

    // ── 5/6/7. Asiento compuesto balancea con variación positiva/negativa/cero ─────────

    private static PostingRule CompoundPurchaseReturnRule(
        Guid payableAccount,
        Guid supplierCreditAccount,
        Guid costVarianceAccount,
        Guid inventoryAccount,
        Guid vatAccount,
        Guid iceAccount
    )
    {
        var rule = EmptyRule("Purchases", "PurchaseReturn");
        // Débitos
        rule.AddLine(payableAccount, AccountNature.Debit, PostingAmountKind.AppliedToPayable);
        rule.AddLine(supplierCreditAccount, AccountNature.Debit, PostingAmountKind.SupplierCredit);
        rule.AddLine(costVarianceAccount, AccountNature.Debit, PostingAmountKind.CostVarianceDebit);
        // Créditos
        rule.AddLine(inventoryAccount, AccountNature.Credit, PostingAmountKind.HistoricalCost);
        rule.AddLine(vatAccount, AccountNature.Credit, PostingAmountKind.TaxVat);
        rule.AddLine(iceAccount, AccountNature.Credit, PostingAmountKind.TaxIce);
        // Misma cuenta de variación, lado crédito — condicional, mutuamente excluyente con la línea débito
        rule.AddLine(
            costVarianceAccount,
            AccountNature.Credit,
            PostingAmountKind.CostVarianceCredit
        );
        return rule;
    }

    [Fact]
    public async Task Asiento_con_variacion_de_costo_positiva_balancea_Sigma_debitos_igual_Sigma_creditos()
    {
        // Ejemplo (g) de §11.3 del diseño: 336.00 = AppliedToPayable; CostVarianceTotal=+45.00
        var payableAccount = Guid.NewGuid();
        var supplierCreditAccount = Guid.NewGuid();
        var costVarianceAccount = Guid.NewGuid();
        var inventoryAccount = Guid.NewGuid();
        var vatAccount = Guid.NewGuid();
        var iceAccount = Guid.NewGuid();
        var rule = CompoundPurchaseReturnRule(
            payableAccount,
            supplierCreditAccount,
            costVarianceAccount,
            inventoryAccount,
            vatAccount,
            iceAccount
        );

        var m = new Mocks(2026, 8);
        m.SetupRule("Purchases", "PurchaseReturn", rule);

        var fact = PurchaseReturnFact(
            appliedToPayable: 336.00m,
            supplierCredit: 0m,
            costVarianceDebit: 45.00m,
            costVarianceCredit: 0m,
            historicalCost: 345.00m,
            returnedVat: 36.00m,
            returnedIce: 0m
        );

        var result = await m.BuildEngine().PostAsync(fact);

        result.IsSuccess.Should().BeTrue();
        var totalDebit = m.Captured!.Lines.Sum(l => l.Debit);
        var totalCredit = m.Captured.Lines.Sum(l => l.Credit);
        totalDebit.Should().Be(381.00m);
        totalCredit.Should().Be(381.00m);
        totalDebit.Should().Be(totalCredit);
        // Solo la línea de variación DÉBITO aparece — la de CRÉDITO se omite (monto resuelto 0)
        m.Captured.Lines.Count(l => l.AccountId == costVarianceAccount).Should().Be(1);
        m.Captured.Lines.Should()
            .Contain(l => l.AccountId == costVarianceAccount && l.Debit == 45.00m);
    }

    [Fact]
    public async Task Asiento_con_variacion_de_costo_negativa_balancea_Sigma_debitos_igual_Sigma_creditos()
    {
        var payableAccount = Guid.NewGuid();
        var supplierCreditAccount = Guid.NewGuid();
        var costVarianceAccount = Guid.NewGuid();
        var inventoryAccount = Guid.NewGuid();
        var vatAccount = Guid.NewGuid();
        var iceAccount = Guid.NewGuid();
        var rule = CompoundPurchaseReturnRule(
            payableAccount,
            supplierCreditAccount,
            costVarianceAccount,
            inventoryAccount,
            vatAccount,
            iceAccount
        );

        var m = new Mocks(2026, 8);
        m.SetupRule("Purchases", "PurchaseReturn", rule);

        // AppliedToPayable(200) + SupplierCredit(0) + CVdebit(0) = 200
        // HistoricalCost(180) + Vat(0) + Ice(0) + CVcredit(20) = 200
        var fact = PurchaseReturnFact(
            appliedToPayable: 200m,
            supplierCredit: 0m,
            costVarianceDebit: 0m,
            costVarianceCredit: 20m,
            historicalCost: 180m,
            returnedVat: 0m,
            returnedIce: 0m
        );

        var result = await m.BuildEngine().PostAsync(fact);

        result.IsSuccess.Should().BeTrue();
        var totalDebit = m.Captured!.Lines.Sum(l => l.Debit);
        var totalCredit = m.Captured.Lines.Sum(l => l.Credit);
        totalDebit.Should().Be(200m);
        totalCredit.Should().Be(200m);
        totalDebit.Should().Be(totalCredit);
        m.Captured.Lines.Count(l => l.AccountId == costVarianceAccount).Should().Be(1);
        m.Captured.Lines.Should()
            .Contain(l => l.AccountId == costVarianceAccount && l.Credit == 20m);
    }

    [Fact]
    public async Task Asiento_con_variacion_de_costo_cero_balancea_sin_linea_de_variacion()
    {
        var payableAccount = Guid.NewGuid();
        var supplierCreditAccount = Guid.NewGuid();
        var costVarianceAccount = Guid.NewGuid();
        var inventoryAccount = Guid.NewGuid();
        var vatAccount = Guid.NewGuid();
        var iceAccount = Guid.NewGuid();
        var rule = CompoundPurchaseReturnRule(
            payableAccount,
            supplierCreditAccount,
            costVarianceAccount,
            inventoryAccount,
            vatAccount,
            iceAccount
        );

        var m = new Mocks(2026, 8);
        m.SetupRule("Purchases", "PurchaseReturn", rule);

        var fact = PurchaseReturnFact(
            appliedToPayable: 300m,
            supplierCredit: 0m,
            costVarianceDebit: 0m,
            costVarianceCredit: 0m,
            historicalCost: 264m,
            returnedVat: 36m,
            returnedIce: 0m
        );

        var result = await m.BuildEngine().PostAsync(fact);

        result.IsSuccess.Should().BeTrue();
        var totalDebit = m.Captured!.Lines.Sum(l => l.Debit);
        var totalCredit = m.Captured.Lines.Sum(l => l.Credit);
        totalDebit.Should().Be(300m);
        totalCredit.Should().Be(300m);
        totalDebit.Should().Be(totalCredit);
        m.Captured.Lines.Should()
            .NotContain(
                l => l.AccountId == costVarianceAccount,
                because: "sin variación de costo (CostVarianceTotal=0) ninguna de las 2 líneas condicionales debe aparecer"
            );
    }
}
