using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Queries;
using ERP.Application.Modules.Accounting.UseCases.Reports;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-REPORTS-09 — pruebas de handler (repositorios mockeados) para Libro Diario/Libro
/// Mayor/Balance de Comprobación. Solo ejercita la lógica de agregación/convención contable de
/// Application; la exclusión real de Draft/Reversed vive en el filtro SQL de
/// <c>JournalEntryRepository</c> (probado por separado en ERP.Infrastructure.Tests, ya que ahí es
/// donde ese filtro realmente se aplica).
/// </summary>
public sealed class GetAccountingReportsUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static Account NewAccount(string code, string name, AccountType type, AccountNature nature) =>
        Account.Create(TenantId, CompanyId, AccountCode.Create(code), name, null, type, nature, true, CreatedBy);

    private static Mock<ICurrentTenant> Tenant()
    {
        var t = new Mock<ICurrentTenant>();
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        return t;
    }

    private static Mock<ICurrentCompany> Company()
    {
        var c = new Mock<ICurrentCompany>();
        c.SetupGet(x => x.CompanyId).Returns(CompanyId);
        return c;
    }

    private static Mock<IJournalEntrySourceResolver> EmptyResolver()
    {
        var r = new Mock<IJournalEntrySourceResolver>();
        r.Setup(x =>
                x.ResolveManyAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<JournalEntrySourceRequest>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, JournalEntrySourceInfo>());
        return r;
    }

    // ── Libro Diario ────────────────────────────────────────────────────

    [Fact]
    public async Task GeneralJournal_suma_debe_y_haber_de_todas_las_lineas_devueltas()
    {
        var cash = NewAccount("1.1.01", "Caja", AccountType.Asset, AccountNature.Debit);
        var sales = NewAccount("4.1.01", "Ventas", AccountType.Income, AccountNature.Credit);

        var entry = JournalEntry.Create(
            TenantId, CompanyId, new DateOnly(2026, 8, 1), Guid.NewGuid(), 2026,
            "Sales", "InvoiceIssued", Guid.NewGuid(), "Factura test", CreatedBy
        );
        entry.AddLine(cash.Id, null, 150m, 0m);
        entry.AddLine(sales.Id, null, 0m, 150m);
        entry.Post(CreatedBy, 1);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetPostedEntriesPageAsync(
                    TenantId, CompanyId,
                    It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                    null, null, 1, 50,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new List<JournalEntry> { entry }, 1));

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { cash, sales });

        var handler = new GetGeneralJournalReportHandler(
            repo.Object, accountRepo.Object, EmptyResolver().Object, Tenant().Object, Company().Object
        );

        var result = await handler.Handle(
            new GetGeneralJournalReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(2);
        result.Value.TotalDebit.Should().Be(150m);
        result.Value.TotalCredit.Should().Be(150m);
    }

    // ── ACCOUNTING-REPORTS-HIERARCHY-SMOKE-01: orden natural end-to-end ───

    [Fact]
    public async Task GeneralLedger_ordena_cuentas_por_codigo_natural_no_lexicografico()
    {
        // Ordinal pondría "1.1.10" antes de "1.1.2" (compara carácter a carácter): confirmamos
        // que el handler usa AccountCodeComparer y no StringComparer.Ordinal.
        var a2 = NewAccount("1.1.2", "Cuenta 2", AccountType.Asset, AccountNature.Debit);
        var a10 = NewAccount("1.1.10", "Cuenta 10", AccountType.Asset, AccountNature.Debit);
        var a1 = NewAccount("1.1.1", "Cuenta 1", AccountType.Asset, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, It.IsAny<DateOnly?>(), It.IsAny<DateOnly>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>());
        repo.Setup(r =>
                r.GetPostedLinesByAccountAsync(
                    TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<JournalEntryLineReportRow>());

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { a10, a2, a1 }); // orden de entrada deliberadamente desordenado

        var handler = new GetGeneralLedgerReportHandler(
            repo.Object, accountRepo.Object, EmptyResolver().Object, Tenant().Object, Company().Object
        );

        var result = await handler.Handle(
            new GetGeneralLedgerReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.Value!.Accounts.Select(a => a.AccountCode).Should().Equal("1.1.1", "1.1.2", "1.1.10");
    }

    [Fact]
    public async Task GeneralLedger_rango_de_codigo_usa_orden_natural_no_ordinal()
    {
        // Con StringComparer.Ordinal, el rango "1.1.2".."1.1.9" excluiría "1.1.10" por error de
        // comparación carácter a carácter. Con orden natural, "1.1.10" > "1.1.9" y queda fuera del
        // rango correctamente por motivo numérico real, no accidental.
        var a2 = NewAccount("1.1.2", "Cuenta 2", AccountType.Asset, AccountNature.Debit);
        var a9 = NewAccount("1.1.9", "Cuenta 9", AccountType.Asset, AccountNature.Debit);
        var a10 = NewAccount("1.1.10", "Cuenta 10", AccountType.Asset, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, It.IsAny<DateOnly?>(), It.IsAny<DateOnly>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>());
        repo.Setup(r =>
                r.GetPostedLinesByAccountAsync(
                    TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<JournalEntryLineReportRow>());

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { a2, a9, a10 });

        var handler = new GetGeneralLedgerReportHandler(
            repo.Object, accountRepo.Object, EmptyResolver().Object, Tenant().Object, Company().Object
        );

        var result = await handler.Handle(
            new GetGeneralLedgerReportQuery(
                new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
                AccountCodeFrom: "1.1.2", AccountCodeTo: "1.1.9"
            ),
            CancellationToken.None
        );

        result.Value!.Accounts.Select(a => a.AccountCode).Should().Equal("1.1.2", "1.1.9");
    }

    [Fact]
    public async Task TrialBalance_ordena_cuentas_por_codigo_natural_no_lexicografico()
    {
        var a2 = NewAccount("1.1.2", "Cuenta 2", AccountType.Asset, AccountNature.Debit);
        var a10 = NewAccount("1.1.10", "Cuenta 10", AccountType.Asset, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, null, It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>());
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [a2.Id] = (10m, 0m),
                    [a10.Id] = (20m, 0m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { a10, a2 });

        var handler = new GetTrialBalanceReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(
            new GetTrialBalanceReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.Value!.Lines.Select(l => l.AccountCode).Should().Equal("1.1.2", "1.1.10");
    }

    [Fact]
    public async Task IncomeStatement_respeta_orden_macro_ingresos_costos_gastos_y_orden_natural_dentro_de_cada_grupo()
    {
        var salesA = NewAccount("4.1.2", "Ventas B", AccountType.Income, AccountNature.Credit);
        var salesB = NewAccount("4.1.10", "Ventas A", AccountType.Income, AccountNature.Credit);
        var cogs = NewAccount("5.1.01", "Costo de ventas", AccountType.Cost, AccountNature.Debit);
        var rent = NewAccount("6.1.02", "Arriendo B", AccountType.Expense, AccountNature.Debit);
        var payroll = NewAccount("6.1.10", "Sueldos A", AccountType.Expense, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [salesA.Id] = (0m, 100m),
                    [salesB.Id] = (0m, 200m),
                    [cogs.Id] = (50m, 0m),
                    [rent.Id] = (10m, 0m),
                    [payroll.Id] = (20m, 0m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { payroll, rent, cogs, salesB, salesA });

        var handler = new GetIncomeStatementReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(
            new GetIncomeStatementReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        // Orden macro: Ingresos → Costos → Gastos, garantizado estructuralmente por las 3
        // propiedades separadas del DTO (no una sola lista mezclada).
        result.Value!.IncomeLines.Select(l => l.AccountCode).Should().Equal("4.1.2", "4.1.10");
        result.Value.CostLines.Select(l => l.AccountCode).Should().Equal("5.1.01");
        result.Value.ExpenseLines.Select(l => l.AccountCode).Should().Equal("6.1.02", "6.1.10");
    }

    [Fact]
    public async Task IncomeStatement_no_muestra_cuenta_agrupadora_sin_saldo_directo_como_movimiento()
    {
        var group = NewAccount("6.1", "Gastos administrativos", AccountType.Expense, AccountNature.Debit);
        var leaf = NewAccount("6.1.01", "Arriendo", AccountType.Expense, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                // La agrupadora nunca recibe líneas de asiento (Posting Engine solo postea a
                // hojas AllowsPosting=true) — no aparece en totals, mismo criterio que produce
                // este dict en producción.
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)> { [leaf.Id] = (50m, 0m) }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { group, leaf });

        var handler = new GetIncomeStatementReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(
            new GetIncomeStatementReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.Value!.ExpenseLines.Should().ContainSingle(l => l.AccountId == leaf.Id);
        result.Value.ExpenseLines.Should().NotContain(l => l.AccountId == group.Id);
    }

    [Fact]
    public async Task BalanceSheet_respeta_orden_macro_activo_pasivo_patrimonio_y_orden_natural_dentro_de_cada_grupo()
    {
        var cashA = NewAccount("1.1.2", "Bancos B", AccountType.Asset, AccountNature.Debit);
        var cashB = NewAccount("1.1.10", "Bancos A", AccountType.Asset, AccountNature.Debit);
        var payable = NewAccount("2.1.01", "Cuentas por pagar", AccountType.Liability, AccountNature.Credit);
        var capitalA = NewAccount("3.1.2", "Capital B", AccountType.Equity, AccountNature.Credit);
        var capitalB = NewAccount("3.1.10", "Capital A", AccountType.Equity, AccountNature.Credit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, null, It.IsAny<DateOnly>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [cashA.Id] = (100m, 0m),
                    [cashB.Id] = (200m, 0m),
                    [payable.Id] = (0m, 150m),
                    [capitalA.Id] = (0m, 50m),
                    [capitalB.Id] = (0m, 100m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { capitalB, capitalA, payable, cashB, cashA });

        var handler = new GetBalanceSheetReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(new GetBalanceSheetReportQuery(new DateOnly(2026, 8, 31)), CancellationToken.None);

        // Orden macro: Activo → Pasivo → Patrimonio, garantizado estructuralmente.
        result.Value!.AssetLines.Select(l => l.AccountCode).Should().Equal("1.1.2", "1.1.10");
        result.Value.LiabilityLines.Select(l => l.AccountCode).Should().Equal("2.1.01");
        result.Value.EquityLines.Select(l => l.AccountCode).Should().Equal("3.1.2", "3.1.10");
    }

    // ── Libro Mayor ─────────────────────────────────────────────────────

    [Fact]
    public async Task GeneralLedger_calcula_saldo_inicial_y_final_para_cuenta_de_naturaleza_deudora()
    {
        var cash = NewAccount("1.1.01", "Caja", AccountType.Asset, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, null, new DateOnly(2026, 7, 31),
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(cash.Id)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [cash.Id] = (500m, 200m), // saldo inicial neto deudor = 300
                }
            );
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(cash.Id)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [cash.Id] = (100m, 50m),
                }
            );
        repo.Setup(r =>
                r.GetPostedLinesByAccountAsync(
                    TenantId, CompanyId, cash.Id,
                    new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new List<JournalEntryLineReportRow>
                {
                    new(Guid.NewGuid(), 10, new DateOnly(2026, 8, 5), "Cobro", "Finance", "CollectionApplied", Guid.NewGuid(), Guid.NewGuid(), 100m, 0m),
                    new(Guid.NewGuid(), 11, new DateOnly(2026, 8, 10), "Pago", "Finance", "SupplierPaymentApplied", Guid.NewGuid(), Guid.NewGuid(), 0m, 50m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { cash });

        var handler = new GetGeneralLedgerReportHandler(
            repo.Object, accountRepo.Object, EmptyResolver().Object, Tenant().Object, Company().Object
        );

        var result = await handler.Handle(
            new GetGeneralLedgerReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), AccountId: cash.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        var account = result.Value!.Accounts.Should().ContainSingle().Subject;
        account.OpeningBalance.Should().Be(300m);
        account.PeriodDebit.Should().Be(100m);
        account.PeriodCredit.Should().Be(50m);
        account.ClosingBalance.Should().Be(350m); // 300 + 100 - 50
        account.Movements.Should().HaveCount(2);
        account.Movements[0].RunningBalance.Should().Be(400m); // 300 + 100
        account.Movements[1].RunningBalance.Should().Be(350m); // 400 - 50
    }

    // ── Balance de Comprobación ─────────────────────────────────────────

    [Fact]
    public async Task TrialBalance_reporta_balanceado_cuando_total_debe_es_igual_a_total_haber()
    {
        var cash = NewAccount("1.1.01", "Caja", AccountType.Asset, AccountNature.Debit);
        var sales = NewAccount("4.1.01", "Ventas", AccountType.Income, AccountNature.Credit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, null, new DateOnly(2026, 7, 31), null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>());
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [cash.Id] = (150m, 0m),
                    [sales.Id] = (0m, 150m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { cash, sales });

        var handler = new GetTrialBalanceReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(
            new GetTrialBalanceReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(2);
        result.Value.TotalPeriodDebit.Should().Be(150m);
        result.Value.TotalPeriodCredit.Should().Be(150m);
        result.Value.IsBalanced.Should().BeTrue();

        var cashLine = result.Value.Lines.Single(l => l.AccountId == cash.Id);
        cashLine.ClosingDebit.Should().Be(150m);
        cashLine.ClosingCredit.Should().Be(0m);
    }

    [Fact]
    public async Task TrialBalance_sin_IncludeZeroMovementAccounts_omite_cuentas_sin_actividad()
    {
        var cash = NewAccount("1.1.01", "Caja", AccountType.Asset, AccountNature.Debit);
        var unused = NewAccount("1.1.02", "Bancos", AccountType.Asset, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, null, It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>());
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [cash.Id] = (100m, 0m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { cash, unused });

        var handler = new GetTrialBalanceReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(
            new GetTrialBalanceReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.Value!.Lines.Should().ContainSingle(l => l.AccountId == cash.Id);
    }

    [Fact]
    public async Task TrialBalance_con_IncludeZeroMovementAccounts_incluye_cuentas_sin_actividad()
    {
        var cash = NewAccount("1.1.01", "Caja", AccountType.Asset, AccountNature.Debit);
        var unused = NewAccount("1.1.02", "Bancos", AccountType.Asset, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, null, It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>());
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [cash.Id] = (100m, 0m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { cash, unused });

        var handler = new GetTrialBalanceReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(
            new GetTrialBalanceReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), IncludeZeroMovementAccounts: true),
            CancellationToken.None
        );

        result.Value!.Lines.Should().HaveCount(2);
        result.Value.Lines.Should().Contain(l => l.AccountId == unused.Id && l.OpeningDebit == 0m && l.ClosingDebit == 0m);
    }

    // ── Estado de Resultados (ACCOUNTING-FINANCIAL-STATEMENTS-10) ─────────

    [Fact]
    public async Task IncomeStatement_calcula_ingresos_costos_gastos_y_utilidad()
    {
        var sales = NewAccount("4.1.01", "Ventas", AccountType.Income, AccountNature.Credit);
        var cogs = NewAccount("5.1.01", "Costo de ventas", AccountType.Cost, AccountNature.Debit);
        var rent = NewAccount("6.1.01", "Arriendo", AccountType.Expense, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [sales.Id] = (0m, 1000m),
                    [cogs.Id] = (600m, 0m),
                    [rent.Id] = (150m, 0m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { sales, cogs, rent });

        var handler = new GetIncomeStatementReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(
            new GetIncomeStatementReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalIncome.Should().Be(1000m);
        result.Value.TotalCost.Should().Be(600m);
        result.Value.GrossProfit.Should().Be(400m);
        result.Value.TotalExpense.Should().Be(150m);
        result.Value.NetProfit.Should().Be(250m);
        result.Value.IncomeLines.Should().ContainSingle(l => l.AccountId == sales.Id && l.Amount == 1000m);
        result.Value.CostLines.Should().ContainSingle(l => l.AccountId == cogs.Id && l.Amount == 600m);
        result.Value.ExpenseLines.Should().ContainSingle(l => l.AccountId == rent.Id && l.Amount == 150m);
    }

    [Fact]
    public async Task IncomeStatement_omite_cuentas_sin_movimiento_en_el_rango()
    {
        var sales = NewAccount("4.1.01", "Ventas", AccountType.Income, AccountNature.Credit);
        var unusedExpense = NewAccount("6.1.02", "Publicidad", AccountType.Expense, AccountNature.Debit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [sales.Id] = (0m, 500m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { sales, unusedExpense });

        var handler = new GetIncomeStatementReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(
            new GetIncomeStatementReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.Value!.ExpenseLines.Should().BeEmpty();
        result.Value.TotalExpense.Should().Be(0m);
    }

    // ── Balance General (ACCOUNTING-FINANCIAL-STATEMENTS-10) ──────────────

    [Fact]
    public async Task BalanceSheet_calcula_activos_pasivos_patrimonio_y_reporta_cuadre()
    {
        var cash = NewAccount("1.1.01", "Caja", AccountType.Asset, AccountNature.Debit);
        var payable = NewAccount("2.1.01", "Cuentas por pagar", AccountType.Liability, AccountNature.Credit);
        var capital = NewAccount("3.1.01", "Capital social", AccountType.Equity, AccountNature.Credit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, null, new DateOnly(2026, 8, 31),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    [cash.Id] = (1000m, 0m),
                    [payable.Id] = (0m, 400m),
                    [capital.Id] = (0m, 600m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { cash, payable, capital });

        var handler = new GetBalanceSheetReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(new GetBalanceSheetReportQuery(new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalAssets.Should().Be(1000m);
        result.Value.TotalLiabilities.Should().Be(400m);
        result.Value.TotalEquity.Should().Be(600m);
        result.Value.Difference.Should().Be(0m);
        result.Value.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task BalanceSheet_reporta_IsBalanced_false_cuando_activos_no_cuadran_con_pasivo_mas_patrimonio()
    {
        var cash = NewAccount("1.1.01", "Caja", AccountType.Asset, AccountNature.Debit);
        var capital = NewAccount("3.1.01", "Capital social", AccountType.Equity, AccountNature.Credit);

        var repo = new Mock<IJournalEntryRepository>();
        repo.Setup(r =>
                r.GetAccountLineTotalsAsync(
                    TenantId, CompanyId, null, It.IsAny<DateOnly>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>
                {
                    // Caja aumentó 1000 por una venta (crédito fue a Ingresos, cuenta que
                    // Balance General no consulta) — sin cierre contable, esto NO cuadra, y es
                    // el comportamiento esperado documentado en GetBalanceSheetReportResponse.
                    [cash.Id] = (1000m, 0m),
                    [capital.Id] = (0m, 600m),
                }
            );

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(a => a.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { cash, capital });

        var handler = new GetBalanceSheetReportHandler(repo.Object, accountRepo.Object, Tenant().Object, Company().Object);

        var result = await handler.Handle(new GetBalanceSheetReportQuery(new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Value!.Difference.Should().Be(400m);
        result.Value.IsBalanced.Should().BeFalse();
    }
}
