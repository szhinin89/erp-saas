using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.Reports;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ACCOUNTING-REPORTS-09 — pruebas de integración (PostgreSQL 16 real vía Testcontainers) para
/// las consultas de reportes agregadas en <see cref="JournalEntryRepository"/>
/// (GetPostedEntriesPageAsync/GetAccountLineTotalsAsync/GetPostedLinesByAccountAsync). Cubre
/// específicamente lo que solo puede probarse a nivel SQL: exclusión de Draft, y que un asiento
/// Reversed (el original) queda fuera mientras su reverso (Posted) sí cuenta — y aislamiento
/// multi-tenant/company. La lógica de agregación pura (saldo inicial/final, convención
/// deudora/acreedora) está cubierta en ERP.Application.Tests con repositorios mockeados.
/// ACCOUNTING-FINANCIAL-STATEMENTS-10 agrega pruebas end-to-end (repositorio real + handler
/// real) de Estado de Resultados/Balance General, mismo criterio de reverso ya validado arriba.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AccountingReportsRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_accounting_reports_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _otherCompanyId;
    private Guid _accountingPeriodId;
    private Guid _createdBy;
    private Guid _cashAccountId;
    private Guid _salesAccountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var otherCompany = Company.CreateManaged(tenant.Id, "1790012345002", "Otra S.A.", createdBy: _createdBy);
        var period = AccountingPeriod.Create(
            tenant.Id, company.Id, 2026, 8, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), _createdBy
        );
        var cash = Account.Create(
            tenant.Id, company.Id, AccountCode.Create("1.1.01"), "Caja", null,
            AccountType.Asset, AccountNature.Debit, true, _createdBy
        );
        var sales = Account.Create(
            tenant.Id, company.Id, AccountCode.Create("4.1.01"), "Ventas", null,
            AccountType.Income, AccountNature.Credit, true, _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, otherCompany);
        db.AccountingPeriods.Add(period);
        db.Accounts.AddRange(cash, sales);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _otherCompanyId = otherCompany.Id;
        _accountingPeriodId = period.Id;
        _cashAccountId = cash.Id;
        _salesAccountId = sales.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid? companyOverride = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyOverride ?? _companyId)
        );
    }

    private JournalEntry BalancedEntry(DateOnly date, decimal amount, Guid companyId) =>
        JournalEntry.Create(
            _tenantId, companyId, date, _accountingPeriodId, 2026,
            "Sales", "InvoiceIssued", Guid.NewGuid(), $"Venta {amount:F2}", _createdBy
        );

    [Fact]
    public async Task GetPostedEntriesPageAsync_excluye_Draft_e_incluye_solo_Posted()
    {
        var posted = BalancedEntry(new DateOnly(2026, 8, 5), 100m, _companyId);
        posted.AddLine(_cashAccountId, null, 100m, 0m);
        posted.AddLine(_salesAccountId, null, 0m, 100m);
        posted.Post(_createdBy, 1);

        var draft = BalancedEntry(new DateOnly(2026, 8, 6), 50m, _companyId);
        draft.AddLine(_cashAccountId, null, 50m, 0m);
        draft.AddLine(_salesAccountId, null, 0m, 50m);
        // Nunca se llama Post() — queda en Draft.

        await using var db = CreateContext();
        db.JournalEntries.AddRange(posted, draft);
        await db.SaveChangesAsync();

        var repo = new JournalEntryRepository(CreateContext());
        var (items, totalCount) = await repo.GetPostedEntriesPageAsync(
            _tenantId, _companyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
            null, null, 1, 50
        );

        totalCount.Should().Be(1);
        items.Should().ContainSingle(e => e.Id == posted.Id);
    }

    [Fact]
    public async Task Asiento_reversado_queda_excluido_y_su_reverso_Posted_cuenta_en_los_totales()
    {
        var original = BalancedEntry(new DateOnly(2026, 8, 5), 200m, _companyId);
        original.AddLine(_cashAccountId, null, 200m, 0m);
        original.AddLine(_salesAccountId, null, 0m, 200m);
        original.Post(_createdBy, 1);

        await using (var db = CreateContext())
        {
            db.JournalEntries.Add(original);
            await db.SaveChangesAsync();
        }

        JournalEntry reversal;
        await using (var db = CreateContext())
        {
            var loaded = await db.JournalEntries.Include(x => x.Lines).FirstAsync(x => x.Id == original.Id);
            reversal = loaded.Reverse(_createdBy, 2, "Error de digitación");
            db.JournalEntries.Add(reversal);
            await db.SaveChangesAsync();
        }

        var repo = new JournalEntryRepository(CreateContext());

        var (items, totalCount) = await repo.GetPostedEntriesPageAsync(
            _tenantId, _companyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
            null, null, 1, 50
        );

        totalCount.Should().Be(1, because: "el asiento original quedó en estado Reversed — solo su reverso (Posted) debe contar");
        items.Should().ContainSingle(e => e.Id == reversal.Id);

        var totals = await repo.GetAccountLineTotalsAsync(
            _tenantId, _companyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null
        );

        // El reverso invierte Débito/Crédito del original: Caja pasa a Crédito 200, Ventas a Débito 200.
        totals[_cashAccountId].TotalDebit.Should().Be(0m);
        totals[_cashAccountId].TotalCredit.Should().Be(200m);
        totals[_salesAccountId].TotalDebit.Should().Be(200m);
        totals[_salesAccountId].TotalCredit.Should().Be(0m);
    }

    [Fact]
    public async Task GetAccountLineTotalsAsync_aisla_por_company_dentro_del_mismo_tenant()
    {
        var mine = BalancedEntry(new DateOnly(2026, 8, 5), 100m, _companyId);
        mine.AddLine(_cashAccountId, null, 100m, 0m);
        mine.AddLine(_salesAccountId, null, 0m, 100m);
        mine.Post(_createdBy, 1);

        // Cuentas propias de la otra Company (Account es CompanyId-scoped — no reutiliza las de _companyId).
        var otherCash = Account.Create(
            _tenantId, _otherCompanyId, AccountCode.Create("1.1.01"), "Caja (otra)", null,
            AccountType.Asset, AccountNature.Debit, true, _createdBy
        );
        var otherSales = Account.Create(
            _tenantId, _otherCompanyId, AccountCode.Create("4.1.01"), "Ventas (otra)", null,
            AccountType.Income, AccountNature.Credit, true, _createdBy
        );
        var otherPeriod = AccountingPeriod.Create(
            _tenantId, _otherCompanyId, 2026, 8, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), _createdBy
        );
        var theirs = JournalEntry.Create(
            _tenantId, _otherCompanyId, new DateOnly(2026, 8, 5), otherPeriod.Id, 2026,
            "Sales", "InvoiceIssued", Guid.NewGuid(), "Venta de otra company", _createdBy
        );
        theirs.AddLine(otherCash.Id, null, 999m, 0m);
        theirs.AddLine(otherSales.Id, null, 0m, 999m);
        theirs.Post(_createdBy, 1);

        await using var db = CreateContext();
        db.Accounts.AddRange(otherCash, otherSales);
        db.AccountingPeriods.Add(otherPeriod);
        db.JournalEntries.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        var repo = new JournalEntryRepository(CreateContext());
        var totals = await repo.GetAccountLineTotalsAsync(
            _tenantId, _companyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null
        );

        totals.Should().ContainKey(_cashAccountId);
        totals[_cashAccountId].TotalDebit.Should().Be(100m);
        totals.Should().NotContainKey(otherCash.Id, because: "GetAccountLineTotalsAsync está scoped a _companyId — nunca debe fugar datos de otra Company");
    }

    // ── ACCOUNTING-FINANCIAL-STATEMENTS-10: Estado de Resultados / Balance General ────────

    [Fact]
    public async Task IncomeStatement_reverso_Posted_de_una_venta_impacta_el_total_de_ingresos()
    {
        var original = BalancedEntry(new DateOnly(2026, 8, 5), 500m, _companyId);
        original.AddLine(_cashAccountId, null, 500m, 0m);
        original.AddLine(_salesAccountId, null, 0m, 500m);
        original.Post(_createdBy, 1);

        await using (var db = CreateContext())
        {
            db.JournalEntries.Add(original);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var loaded = await db.JournalEntries.Include(x => x.Lines).FirstAsync(x => x.Id == original.Id);
            var reversal = loaded.Reverse(_createdBy, 2, "Factura anulada");
            db.JournalEntries.Add(reversal);
            await db.SaveChangesAsync();
        }

        var handler = new GetIncomeStatementReportHandler(
            new JournalEntryRepository(CreateContext()),
            new AccountRepository(CreateContext()),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId)
        );

        var result = await handler.Handle(
            new GetIncomeStatementReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        // El asiento original queda Reversed (excluido) — solo su reverso Posted cuenta, mismo
        // criterio ya documentado/probado en ACCOUNTING-REPORTS-09 para Libro Mayor/Balance de
        // Comprobación: el reverso invierte Sales de Crédito 500 a Débito 500, así que el total
        // de Ingresos del período pasa de +500 (si el original no se hubiera reversado) a -500.
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalIncome.Should().Be(-500m);
    }

    [Fact]
    public async Task BalanceSheet_reverso_Posted_impacta_el_saldo_acumulado_de_la_cuenta()
    {
        var original = BalancedEntry(new DateOnly(2026, 8, 5), 300m, _companyId);
        original.AddLine(_cashAccountId, null, 300m, 0m);
        original.AddLine(_salesAccountId, null, 0m, 300m);
        original.Post(_createdBy, 1);

        await using (var db = CreateContext())
        {
            db.JournalEntries.Add(original);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var loaded = await db.JournalEntries.Include(x => x.Lines).FirstAsync(x => x.Id == original.Id);
            var reversal = loaded.Reverse(_createdBy, 2, "Cobro duplicado");
            db.JournalEntries.Add(reversal);
            await db.SaveChangesAsync();
        }

        var handler = new GetBalanceSheetReportHandler(
            new JournalEntryRepository(CreateContext()),
            new AccountRepository(CreateContext()),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId)
        );

        var result = await handler.Handle(
            new GetBalanceSheetReportQuery(new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        // Caja (Activo, naturaleza deudora): el reverso deja Débito=0/Crédito=300 (solo el
        // reverso cuenta, el original quedó Reversed) → saldo = 0 - 300 = -300.
        result.IsSuccess.Should().BeTrue();
        var cashLine = result.Value!.AssetLines.SingleOrDefault(l => l.AccountId == _cashAccountId);
        cashLine.Should().NotBeNull();
        cashLine!.Amount.Should().Be(-300m);
    }

    [Fact]
    public async Task BalanceSheet_aisla_por_company_dentro_del_mismo_tenant()
    {
        var mine = BalancedEntry(new DateOnly(2026, 8, 5), 100m, _companyId);
        mine.AddLine(_cashAccountId, null, 100m, 0m);
        mine.AddLine(_salesAccountId, null, 0m, 100m);
        mine.Post(_createdBy, 1);

        var otherCash = Account.Create(
            _tenantId, _otherCompanyId, AccountCode.Create("1.1.01"), "Caja (otra)", null,
            AccountType.Asset, AccountNature.Debit, true, _createdBy
        );
        var otherSales = Account.Create(
            _tenantId, _otherCompanyId, AccountCode.Create("4.1.01"), "Ventas (otra)", null,
            AccountType.Income, AccountNature.Credit, true, _createdBy
        );
        var otherPeriod = AccountingPeriod.Create(
            _tenantId, _otherCompanyId, 2026, 8, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), _createdBy
        );
        var theirs = JournalEntry.Create(
            _tenantId, _otherCompanyId, new DateOnly(2026, 8, 5), otherPeriod.Id, 2026,
            "Sales", "InvoiceIssued", Guid.NewGuid(), "Venta de otra company", _createdBy
        );
        theirs.AddLine(otherCash.Id, null, 999m, 0m);
        theirs.AddLine(otherSales.Id, null, 0m, 999m);
        theirs.Post(_createdBy, 1);

        await using (var db = CreateContext())
        {
            db.Accounts.AddRange(otherCash, otherSales);
            db.AccountingPeriods.Add(otherPeriod);
            db.JournalEntries.AddRange(mine, theirs);
            await db.SaveChangesAsync();
        }

        var handler = new GetBalanceSheetReportHandler(
            new JournalEntryRepository(CreateContext()),
            new AccountRepository(CreateContext()),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId)
        );

        var result = await handler.Handle(
            new GetBalanceSheetReportQuery(new DateOnly(2026, 8, 31)),
            CancellationToken.None
        );

        result.Value!.AssetLines.Should().ContainSingle(l => l.AccountId == _cashAccountId && l.Amount == 100m);
        result.Value.AssetLines.Should().NotContain(l => l.AccountId == otherCash.Id, "Balance General está scoped a _companyId — nunca debe fugar datos de otra Company");
    }

    // ── ACCOUNTING-REPORTS-QUERY-TRANSLATION-BUG-11E ──────────────────────
    // GetPostedLinesByAccountAsync (Libro Mayor) proyectaba a JournalEntryLineReportRow (un
    // `record`, no una entidad EF) y recién DESPUÉS aplicaba .OrderBy(r => r.EntryDate) — Npgsql/
    // EF Core no puede traducir un OrderBy sobre un miembro de un objeto recién construido en la
    // proyección anterior ("could not be translated"), y este caso NUNCA se detectó antes porque
    // los tests de Libro Mayor existentes (GetAccountingReportsUseCasesTests) mockean
    // IJournalEntryRepository por completo — nunca ejercitan la traducción SQL real. Este test usa
    // Postgres real (Testcontainers) exactamente para cerrar ese hueco: si la query vuelve a
    // quedar en una forma no traducible, este test falla con la misma excepción que vio el
    // usuario en producción, en vez de pasar en silencio como el mock.
    [Fact]
    public async Task GetPostedLinesByAccountAsync_es_traducible_a_SQL_y_devuelve_movimientos_ordenados()
    {
        var first = BalancedEntry(new DateOnly(2026, 8, 5), 100m, _companyId);
        first.AddLine(_cashAccountId, null, 100m, 0m);
        first.AddLine(_salesAccountId, null, 0m, 100m);
        first.Post(_createdBy, 1);

        var second = BalancedEntry(new DateOnly(2026, 8, 10), 50m, _companyId);
        second.AddLine(_cashAccountId, null, 50m, 0m);
        second.AddLine(_salesAccountId, null, 0m, 50m);
        second.Post(_createdBy, 2);

        await using (var db = CreateContext())
        {
            db.JournalEntries.AddRange(first, second);
            await db.SaveChangesAsync();
        }

        var repo = new JournalEntryRepository(CreateContext());

        var rows = await repo.GetPostedLinesByAccountAsync(
            _tenantId, _companyId, _cashAccountId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)
        );

        rows.Should().HaveCount(2);
        rows[0].EntryNumber.Should().Be(1, "el resultado debe llegar ordenado por EntryDate/EntryNumber, no en orden de inserción");
        rows[1].EntryNumber.Should().Be(2);

        var handler = new GetGeneralLedgerReportHandler(
            repo,
            new AccountRepository(CreateContext()),
            new NoOpSourceResolver(),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId)
        );

        var result = await handler.Handle(
            new GetGeneralLedgerReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), AccountId: _cashAccountId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Accounts.Should().ContainSingle().Which.Movements.Should().HaveCount(2);
    }

    private sealed class NoOpSourceResolver : ERP.Application.Modules.Accounting.Queries.IJournalEntrySourceResolver
    {
        public Task<IReadOnlyDictionary<Guid, ERP.Application.Modules.Accounting.Queries.JournalEntrySourceInfo>> ResolveManyAsync(
            Guid tenantId,
            Guid companyId,
            IReadOnlyList<ERP.Application.Modules.Accounting.Queries.JournalEntrySourceRequest> requests,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ERP.Application.Modules.Accounting.Queries.JournalEntrySourceInfo>>(
                new Dictionary<Guid, ERP.Application.Modules.Accounting.Queries.JournalEntrySourceInfo>()
            );
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
