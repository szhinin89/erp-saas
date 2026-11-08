using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.Accounts;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CHART-INTEGRITY-01 — PostgreSQL 16 real (Testcontainers): <see cref="AccountUsageReader"/>
/// (ocho familias, vigentes frente a históricas, aislamiento tenant/empresa, asientos publicados,
/// borradores, descartes y reversos) y serialización de las operaciones del Plan de Cuentas con el
/// lock <c>pg_advisory_xact_lock</c> de <see cref="AccountRepository.AcquireChartLockAsync"/>.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AccountChartIntegrityPostgreSqlTests : IAsyncLifetime
{
    private static readonly TimeSpan BlockedProbe = TimeSpan.FromMilliseconds(700);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_account_chart_integrity_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    // ── Lector: familias ────────────────────────────────────────────────

    [Fact]
    public async Task Lector_devuelve_las_siete_familias_con_configuraciones_activas_e_inactivas()
    {
        var (tenantId, companyId) = await NewCompanyAsync();
        var account = await AddAccountAsync(tenantId, companyId, "1.1.01", true);

        await using (var db = CreateContext(tenantId, companyId))
        {
            var rule = PostingRule.Create(tenantId, companyId, "Sales", "InvoiceIssued", _userId);
            rule.AddLine(account.Id, AccountNature.Debit, PostingAmountKind.Subtotal);
            var method = PaymentMethod.Create(tenantId, $"PM{Suffix()}", "Transferencia", false, false, 1, _userId);
            method.Disable(_userId);
            var branch = NewBranch(tenantId, companyId);
            var register = CashRegister.Create(tenantId, companyId, branch.Id, "C1", "Caja Matriz", _userId);
            register.SetAccountingAccount(account.Id, _userId);
            register.Disable(_userId);
            var cashReason = CashMovementReason.Create(tenantId, companyId, "GASTO", "Gasto menor",
                CashMovementType.ManualExpense, 1, _userId);
            cashReason.SetAccountingAccount(account.Id, _userId);
            var type = ExpenseCategoryNode.CreateType(tenantId, companyId, "T1", "Operativos", _userId);
            var category = ExpenseCategoryNode.CreateCategory(tenantId, companyId, type, "C1", "Servicios", _userId);
            var subcategory = ExpenseCategoryNode.CreateSubcategory(tenantId, companyId, category, "S1",
                "Luz", account.Id, _userId);
            subcategory.SetActive(false, _userId);
            var bank = Bank.Create(tenantId, $"B{Suffix()}", "Banco Prueba", "Prueba", _userId);
            var reason = InventoryAdjustmentReason.Create(tenantId, companyId, "MERMA", "Merma", "Egreso",
                false, 1, _userId);
            reason.Disable(_userId);

            db.PostingRules.Add(rule);
            db.PaymentMethods.Add(method);
            db.Branches.Add(branch);
            db.CashRegisters.Add(register);
            db.CashMovementReasons.Add(cashReason);
            db.ExpenseCategoryNodes.AddRange(type, category, subcategory);
            db.Banks.Add(bank);
            db.InventoryAdjustmentReasons.Add(reason);
            await db.SaveChangesAsync();

            db.PaymentMethodAccounts.Add(PaymentMethodAccount.Create(tenantId, companyId, method.Id, account.Id, _userId));
            var bankAccount = CompanyBankAccount.Create(tenantId, companyId, bank.Id, BankAccountType.Checking,
                "2200123456", "Banco Prueba CTE", account.Id, _userId);
            bankAccount.Disable(_userId);
            db.CompanyBankAccounts.Add(bankAccount);
            db.InventoryAdjustmentReasonAccounts.Add(
                InventoryAdjustmentReasonAccount.Create(tenantId, companyId, reason.Id, account.Id, _userId));
            await db.SaveChangesAsync();
        }

        var references = await ReadReferencesAsync(tenantId, companyId, account.Id);

        references.Select(r => r.Family).Should().Equal(
            AccountReferenceFamily.PostingRule,
            AccountReferenceFamily.PaymentMethod,
            AccountReferenceFamily.CashRegister,
            AccountReferenceFamily.CashMovementReason,
            AccountReferenceFamily.ExpenseCategory,
            AccountReferenceFamily.CompanyBankAccount,
            AccountReferenceFamily.InventoryAdjustmentReason
        );
        references.Select(r => r.Label).Should().Equal(
            "Sales/InvoiceIssued", "Transferencia", "Caja Matriz", "Gasto menor", "Luz",
            "Banco Prueba CTE", "Merma");
    }

    [Fact]
    public async Task Lector_excluye_reglas_inactivas_y_cuentas_sin_vinculacion()
    {
        var (tenantId, companyId) = await NewCompanyAsync();
        var account = await AddAccountAsync(tenantId, companyId, "1.1.01", true);
        var unrelated = await AddAccountAsync(tenantId, companyId, "1.1.02", true);

        await using (var db = CreateContext(tenantId, companyId))
        {
            var rule = PostingRule.Create(tenantId, companyId, "Sales", "InvoiceIssued", _userId);
            rule.AddLine(account.Id, AccountNature.Debit, PostingAmountKind.Subtotal);
            rule.Disable(_userId);
            var branch = NewBranch(tenantId, companyId);
            db.PostingRules.Add(rule);
            db.Branches.Add(branch);
            // Caja sin cuenta configurada: no es vinculación.
            db.CashRegisters.Add(CashRegister.Create(tenantId, companyId, branch.Id, "C1", "Caja", _userId));
            await db.SaveChangesAsync();
        }

        (await ReadReferencesAsync(tenantId, companyId, account.Id)).Should().BeEmpty();
        (await ReadReferencesAsync(tenantId, companyId, unrelated.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Lector_aisla_por_empresa_y_por_tenant()
    {
        var (tenantId, companyId) = await NewCompanyAsync();
        var otherCompanyId = await NewCompanyInTenantAsync(tenantId);
        var (otherTenantId, otherTenantCompanyId) = await NewCompanyAsync();
        var otherCompanyAccount = await AddAccountAsync(tenantId, otherCompanyId, "1.1.01", true);
        var otherTenantAccount = await AddAccountAsync(otherTenantId, otherTenantCompanyId, "1.1.01", true);
        await AddCashRegisterAsync(tenantId, otherCompanyId, otherCompanyAccount.Id);
        await AddCashRegisterAsync(otherTenantId, otherTenantCompanyId, otherTenantAccount.Id);
        await AddPostedEntryAsync(tenantId, otherCompanyId, otherCompanyAccount.Id);

        (await ReadReferencesAsync(tenantId, companyId, otherCompanyAccount.Id)).Should().BeEmpty();
        (await ReadReferencesAsync(tenantId, otherTenantCompanyId, otherTenantAccount.Id)).Should().BeEmpty();
        (await HasPostedAsync(tenantId, companyId, otherCompanyAccount.Id)).Should().BeFalse();
        // Control positivo: en su propio ámbito las mismas vinculaciones sí se ven.
        (await ReadReferencesAsync(tenantId, otherCompanyId, otherCompanyAccount.Id)).Should().ContainSingle();
        (await ReadReferencesAsync(otherTenantId, otherTenantCompanyId, otherTenantAccount.Id)).Should().ContainSingle();
        (await HasPostedAsync(tenantId, otherCompanyId, otherCompanyAccount.Id)).Should().BeTrue();
    }

    // ── Lector: asientos ────────────────────────────────────────────────

    [Fact]
    public async Task Asientos_publicados_y_reversos_cuentan_borradores_y_descartes_no()
    {
        var (tenantId, companyId) = await NewCompanyAsync();
        var posted = await AddAccountAsync(tenantId, companyId, "1.1.01", true);
        var draftOnly = await AddAccountAsync(tenantId, companyId, "1.1.02", true);
        var discardedOnly = await AddAccountAsync(tenantId, companyId, "1.1.03", true);
        var counterpart = await AddAccountAsync(tenantId, companyId, "4.1.01", true);
        var periodId = await AddPeriodAsync(tenantId, companyId);

        var originalId = await AddPostedEntryAsync(tenantId, companyId, posted.Id, counterpart.Id, periodId);
        await using (var db = CreateContext(tenantId, companyId))
        {
            var original = await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == originalId);
            var number = await new JournalEntrySequenceRepository(db).ReserveNextNumberAsync(tenantId, companyId, 2026, CancellationToken.None);
            db.JournalEntries.Add(original.Reverse(_userId, number, "Prueba", new DateOnly(2026, 7, 20), periodId, 2026));

            db.JournalEntries.Add(Manual(tenantId, companyId, periodId, draftOnly.Id, counterpart.Id));
            var discarded = Manual(tenantId, companyId, periodId, discardedOnly.Id, counterpart.Id);
            discarded.Discard(_userId, "Prueba");
            db.JournalEntries.Add(discarded);
            await db.SaveChangesAsync();

            (await db.JournalEntries.CountAsync(e => e.OriginalJournalEntryId == originalId
                && e.Status == JournalEntryStatus.Posted)).Should().Be(1, "el reverso queda publicado");
        }

        (await HasPostedAsync(tenantId, companyId, posted.Id)).Should().BeTrue();
        (await HasPostedAsync(tenantId, companyId, draftOnly.Id)).Should().BeFalse();
        (await HasPostedAsync(tenantId, companyId, discardedOnly.Id)).Should().BeFalse();
    }

    // ── Serialización ───────────────────────────────────────────────────

    [Fact]
    public async Task Lock_del_plan_serializa_por_empresa_y_no_bloquea_otra_empresa()
    {
        var (tenantId, companyId) = await NewCompanyAsync();
        var otherCompanyId = await NewCompanyInTenantAsync(tenantId);

        await using var holder = CreateContext(tenantId, companyId);
        await using var holderTx = await holder.Database.BeginTransactionAsync();
        await new AccountRepository(holder).AcquireChartLockAsync(companyId);

        await using var other = CreateContext(tenantId, otherCompanyId);
        await using (await other.Database.BeginTransactionAsync())
        {
            await new AccountRepository(other).AcquireChartLockAsync(otherCompanyId)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }

        await using var waiter = CreateContext(tenantId, companyId);
        await using var waiterTx = await waiter.Database.BeginTransactionAsync();
        var blocked = new AccountRepository(waiter).AcquireChartLockAsync(companyId);
        await Task.Delay(BlockedProbe);
        blocked.IsCompleted.Should().BeFalse("la misma empresa espera al titular del lock");

        await holderTx.CommitAsync();
        await blocked.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Lock_exige_transaccion_activa()
    {
        var (tenantId, companyId) = await NewCompanyAsync();
        await using var db = CreateContext(tenantId, companyId);

        var act = () => new AccountRepository(db).AcquireChartLockAsync(companyId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Crear_subcuenta_y_marcar_movimiento_en_el_padre_se_serializan_sin_violar_I1()
    {
        var (tenantId, companyId) = await NewCompanyAsync();
        var parent = await AddAccountAsync(tenantId, companyId, "1", false);

        // Un tercero retiene el lock: ambas operaciones quedan en espera y luego se ejecutan una
        // tras otra, cada una leyendo lo que la anterior confirmó.
        await using var holder = CreateContext(tenantId, companyId);
        await using var holderTx = await holder.Database.BeginTransactionAsync();
        await new AccountRepository(holder).AcquireChartLockAsync(companyId);

        await using var createDb = CreateContext(tenantId, companyId);
        await using var updateDb = CreateContext(tenantId, companyId);
        var create = CreateHandler(createDb, tenantId, companyId).Handle(
            new CreateAccountCommand("1.1", "Subcuenta", parent.Id, AccountType.Asset, AccountNature.Debit, true),
            CancellationToken.None);
        var update = UpdateHandler(updateDb, tenantId, companyId).Handle(
            new UpdateAccountCommand(parent.Id, "Activo", null, true), CancellationToken.None);
        await Task.Delay(BlockedProbe);
        create.IsCompleted.Should().BeFalse();
        update.IsCompleted.Should().BeFalse();

        await holderTx.CommitAsync();
        var results = await Task.WhenAll(create, update).WaitAsync(TimeSpan.FromSeconds(10));

        results.Count(r => r.IsSuccess).Should().Be(1);
        await using var verify = CreateContext(tenantId, companyId);
        var chart = await verify.Accounts.ToListAsync();
        var storedParent = chart.Single(a => a.Id == parent.Id);
        (storedParent.AllowsPosting && chart.Any(a => a.ParentAccountId == parent.Id))
            .Should().BeFalse("una cuenta con subcuentas no admite movimiento (I1)");
    }

    // ── Apoyo ───────────────────────────────────────────────────────────

    private CreateAccountHandler CreateHandler(ErpDbContext db, Guid tenantId, Guid companyId) =>
        new(new AccountRepository(db), new AccountUsageReader(db), new UnitOfWork(db),
            new FixedCurrentTenant(tenantId), new FixedCurrentCompany(companyId),
            new FixedCurrentUser(_userId), new PostgresDatabaseExceptionTranslator());

    private UpdateAccountHandler UpdateHandler(ErpDbContext db, Guid tenantId, Guid companyId) =>
        new(new AccountRepository(db), new AccountUsageReader(db), new UnitOfWork(db),
            new FixedCurrentTenant(tenantId), new FixedCurrentCompany(companyId), new FixedCurrentUser(_userId));

    private async Task<IReadOnlyList<AccountConfigurationReference>> ReadReferencesAsync(
        Guid tenantId, Guid companyId, Guid accountId)
    {
        await using var db = CreateContext(tenantId, companyId);
        return await new AccountUsageReader(db).GetConfigurationReferencesAsync(tenantId, companyId, accountId, CancellationToken.None);
    }

    private async Task<bool> HasPostedAsync(Guid tenantId, Guid companyId, Guid accountId)
    {
        await using var db = CreateContext(tenantId, companyId);
        return await new AccountUsageReader(db).HasPostedJournalLinesAsync(tenantId, companyId, accountId, CancellationToken.None);
    }

    private async Task<(Guid TenantId, Guid CompanyId)> NewCompanyAsync()
    {
        await using var db = CreateContext(Guid.Empty, Guid.Empty);
        var tenant = Tenant.Create("Tenant prueba", $"t-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, NextRuc(), "Empresa A", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return (tenant.Id, company.Id);
    }

    private async Task<Guid> NewCompanyInTenantAsync(Guid tenantId)
    {
        await using var db = CreateContext(tenantId, Guid.Empty);
        var company = Company.CreateManaged(tenantId, NextRuc(), "Empresa B", createdBy: _userId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private async Task<Account> AddAccountAsync(Guid tenantId, Guid companyId, string code, bool allowsPosting)
    {
        await using var db = CreateContext(tenantId, companyId);
        var account = Account.Create(tenantId, companyId, AccountCode.Create(code), $"Cuenta {code}", null,
            AccountType.Asset, AccountNature.Debit, allowsPosting, _userId);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private async Task AddCashRegisterAsync(Guid tenantId, Guid companyId, Guid accountId)
    {
        await using var db = CreateContext(tenantId, companyId);
        var branch = NewBranch(tenantId, companyId);
        var register = CashRegister.Create(tenantId, companyId, branch.Id, "C1", "Caja", _userId);
        register.SetAccountingAccount(accountId, _userId);
        db.Branches.Add(branch);
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddPeriodAsync(Guid tenantId, Guid companyId)
    {
        await using var db = CreateContext(tenantId, companyId);
        var period = AccountingPeriod.Create(tenantId, companyId, 2026, 7, new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31), _userId);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
    }

    private async Task<Guid> AddPostedEntryAsync(Guid tenantId, Guid companyId, Guid debitAccountId,
        Guid? creditAccountId = null, Guid? periodId = null)
    {
        periodId ??= await AddPeriodAsync(tenantId, companyId);
        creditAccountId ??= (await AddAccountAsync(tenantId, companyId, $"4.{Suffix()}", true)).Id;
        await using var db = CreateContext(tenantId, companyId);
        var number = await new JournalEntrySequenceRepository(db).ReserveNextNumberAsync(tenantId, companyId, 2026, CancellationToken.None);
        var entry = JournalEntry.Create(tenantId, companyId, new DateOnly(2026, 7, 15), periodId, 2026,
            "Sales", "InvoiceIssued", Guid.NewGuid(), "Asiento original", _userId);
        entry.AddLine(debitAccountId, "Débito", 100m, 0m);
        entry.AddLine(creditAccountId.Value, "Crédito", 0m, 100m);
        entry.Post(_userId, number);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private JournalEntry Manual(Guid tenantId, Guid companyId, Guid periodId, Guid debitId, Guid creditId) =>
        JournalEntry.CreateManual(tenantId, companyId, Guid.NewGuid(), new DateOnly(2026, 7, 16), periodId, 2026,
            "Manual de prueba", null,
            new[] { new ManualJournalLine(debitId, null, 10m, 0m), new ManualJournalLine(creditId, null, 0m, 10m) },
            2, _userId);

    private Branch NewBranch(Guid tenantId, Guid companyId) =>
        Branch.Create(tenantId, "Matriz", "Dirección", $"B{Suffix()}",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
            email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
            managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
            latitude: null, longitude: null, openingDate: null, internalNotes: null,
            isMainBranch: true, createdBy: _userId, companyId: companyId);

    private static int _ruc = Random.Shared.Next(1_000_000, 9_000_000);

    private static string NextRuc() => $"17{Interlocked.Increment(ref _ruc):D8}001";

    private static string Suffix() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

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

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
