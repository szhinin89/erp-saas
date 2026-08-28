using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// ACCOUNTING-INITIAL-CHART-SEED-11 — cubre lo que el diagnóstico previo (ACCOUNTING-DATA-SEED-
/// AND-SMOKE-10G) encontró vacío: sin este step, ninguna Company tiene Plan de Cuentas ni
/// AccountingPeriod, así que el Posting Engine nunca encuentra una PostingRule (no hay cuentas a
/// las que apuntar) — cero JournalEntry aun con documentos operativos reales. Usa InMemory (no
/// Testcontainers): el step solo hace LINQ/Add/SaveChanges estándar, sin SQL específico de
/// Postgres.
/// </summary>
public sealed class AccountingBootstrapStepTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private ErpDbContext NewDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)
            )
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    [Fact]
    public async Task Primera_ejecucion_crea_plantilla_retail_y_un_periodo_anual_abierto()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var accounts = await db.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        accounts.Should().HaveCount(AccountingBootstrapStep.RetailChartAccountCount);
        accounts.Should().OnlyContain(a => a.IsActive);
        accounts
            .Where(a => a.ParentAccountId is null)
            .Should()
            .OnlyContain(a => !a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "1.1.01.001" && a.Name == "Caja general");
        accounts.Should().Contain(a => a.Code.Value == "1.1.01.002" && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "1.1.03.002" && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "4.1.01.002" && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "6.5.01.001" && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "1" && !a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "2" && !a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "4.1.01" && !a.AllowsPosting);
        var cashParent = accounts.Single(a => a.Code.Value == "1.1.01");
        accounts
            .Single(a => a.Code.Value == "1.1.01.001")
            .ParentAccountId.Should()
            .Be(cashParent.Id);
        accounts
            .Where(a => a.AccountType is AccountType.Asset or AccountType.Cost or AccountType.Expense)
            .Should()
            .OnlyContain(a => a.Nature == AccountNature.Debit);
        accounts
            .Where(a => a.AccountType is AccountType.Liability or AccountType.Equity or AccountType.Income)
            .Should()
            .OnlyContain(a => a.Nature == AccountNature.Credit);

        var periods = await db.AccountingPeriods.Where(p => p.CompanyId == _companyId).ToListAsync();
        periods.Should().ContainSingle();
        periods[0].FiscalYear.Should().Be(DateTime.UtcNow.Year);
        periods[0].StartDate.Should().Be(new DateOnly(DateTime.UtcNow.Year, 1, 1));
        periods[0].EndDate.Should().Be(new DateOnly(DateTime.UtcNow.Year, 12, 31));
        periods[0].Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task Ejecutar_dos_veces_no_duplica_cuentas_ni_periodo()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        (await verifyDb.Accounts.CountAsync(a => a.CompanyId == _companyId))
            .Should()
            .Be(AccountingBootstrapStep.RetailChartAccountCount);
        (await verifyDb.AccountingPeriods.CountAsync(p => p.CompanyId == _companyId)).Should().Be(1);
    }

    [Fact]
    public async Task Si_falta_solo_una_cuenta_crea_unicamente_la_faltante()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var toRemove = await db.Accounts.SingleAsync(a =>
                a.CompanyId == _companyId && a.Code.Value == "6.1.01.001"
            );
            db.Accounts.Remove(toRemove);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var accounts = await verifyDb.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        accounts.Should().HaveCount(AccountingBootstrapStep.RetailChartAccountCount);
        accounts.Should()
            .Contain(a => a.Code.Value == "6.1.01.001" && a.Name == "Gastos administrativos generales");
    }

    [Fact]
    public async Task Si_empresa_tenia_seed_minimo_crea_solo_las_cuentas_retail_faltantes()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var legacyAccounts = new[]
            {
                ("1.1.01.001", "Caja General Legacy", AccountType.Asset, AccountNature.Debit),
                ("1.1.02.001", "Bancos Legacy", AccountType.Asset, AccountNature.Debit),
                ("1.1.03.001", "Cuentas por cobrar clientes Legacy", AccountType.Asset, AccountNature.Debit),
                ("1.1.04.001", "Inventario mercaderías Legacy", AccountType.Asset, AccountNature.Debit),
                ("1.1.05.001", "IVA crédito tributario Legacy", AccountType.Asset, AccountNature.Debit),
                ("2.1.01.001", "Cuentas por pagar proveedores Legacy", AccountType.Liability, AccountNature.Credit),
                ("2.1.02.001", "IVA por pagar Legacy", AccountType.Liability, AccountNature.Credit),
                ("2.1.03.001", "ICE por pagar Legacy", AccountType.Liability, AccountNature.Credit),
                ("3.1.01.001", "Capital Legacy", AccountType.Equity, AccountNature.Credit),
                ("3.1.02.001", "Resultados acumulados Legacy", AccountType.Equity, AccountNature.Credit),
                ("4.1.01.001", "Ventas Legacy", AccountType.Income, AccountNature.Credit),
                ("5.1.01.001", "Costo de ventas Legacy", AccountType.Cost, AccountNature.Debit),
                ("6.1.01.001", "Gastos administrativos Legacy", AccountType.Expense, AccountNature.Debit),
            };

            foreach (var (code, name, type, nature) in legacyAccounts)
            {
                db.Accounts.Add(
                    ERP.Domain.Modules.Accounting.Entities.Account.Create(
                        _tenantId,
                        _companyId,
                        ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create(code),
                        name,
                        parentAccountId: null,
                        accountType: type,
                        nature: nature,
                        allowsPosting: true,
                        createdBy: _actorId
                    )
                );
            }
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var accounts = await verifyDb.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        accounts.Should().HaveCount(AccountingBootstrapStep.RetailChartAccountCount);
        accounts.Should().ContainSingle(a => a.Code.Value == "1.1.01.001");
        accounts.Single(a => a.Code.Value == "1.1.01.001").Name.Should().Be("Caja General Legacy");
        accounts.Single(a => a.Code.Value == "1.1.01.001").ParentAccountId.Should().BeNull();
        accounts.Should().Contain(a => a.Code.Value == "1.1.01.002" && a.Name == "Caja chica");
        accounts.Should().Contain(a => a.Code.Value == "4.1.01.002" && a.Name == "Ventas tarifa 0%");
    }

    [Fact]
    public async Task No_sobrescribe_cuenta_existente_editada_por_usuario()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            db.Accounts.Add(
                ERP.Domain.Modules.Accounting.Entities.Account.Create(
                    _tenantId,
                    _companyId,
                    ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.01.001"),
                    "Caja editada por admin",
                    parentAccountId: null,
                    accountType: AccountType.Asset,
                    nature: AccountNature.Debit,
                    allowsPosting: false,
                    createdBy: _actorId
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var edited = await verifyDb.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code.Value == "1.1.01.001"
        );
        edited.Name.Should().Be("Caja editada por admin");
        edited.AllowsPosting.Should().BeFalse();
        edited.ParentAccountId.Should().BeNull();
    }

    [Fact]
    public async Task Primera_ejecucion_crea_7_posting_rules_con_al_menos_2_lineas_cada_una()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var rules = await db
            .PostingRules.Include(r => r.Lines)
            .Where(r => r.CompanyId == _companyId)
            .ToListAsync();

        rules.Should().HaveCount(6);
        rules.Should().OnlyContain(r => r.IsActive);
        rules.Should().OnlyContain(r => r.Lines.Count >= 2);
        rules
            .Select(r => (r.SourceModule, r.FactType))
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    ("Sales", "InvoiceIssued"),
                    ("Sales", "CostOfGoodsSold"),
                    ("Sales", "CostOfGoodsSoldReversed"),
                    ("Purchases", "InvoiceReceived"),
                    ("Purchases", "PurchaseCreditNoteAuthorized"),
                    ("Finance", "CollectionApplied"),
                }
            );
        // PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — "Finance"/"SupplierPaymentApplied" ya no se siembra
        // (sin RegisterPaymentCommand/traductor que lo dispare, sería configuración muerta).
        rules.Should().NotContain(r => r.SourceModule == "Finance" && r.FactType == "SupplierPaymentApplied");
        rules.Should()
            .NotContain(r => r.SourceModule == "Purchases" && r.FactType == "PurchaseCreditNoteCancelled");
    }

    [Fact]
    public async Task No_crea_posting_rule_si_una_cuenta_requerida_no_permite_asiento()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            db.Accounts.Add(
                ERP.Domain.Modules.Accounting.Entities.Account.Create(
                    _tenantId,
                    _companyId,
                    ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.01.001"),
                    "Caja no postable",
                    parentAccountId: null,
                    accountType: AccountType.Asset,
                    nature: AccountNature.Debit,
                    allowsPosting: false,
                    createdBy: _actorId
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb.PostingRules.Where(r => r.CompanyId == _companyId).ToListAsync();
        rules.Should()
            .NotContain(r => r.SourceModule == "Finance" && r.FactType == "CollectionApplied");
        rules.Should().Contain(r => r.SourceModule == "Sales" && r.FactType == "InvoiceIssued");
    }

    [Fact]
    public async Task Cada_posting_rule_referencia_solo_cuentas_del_plan_sembrado()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var accountIds = (await db.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync())
            .Select(a => a.Id)
            .ToHashSet();
        var rules = await db
            .PostingRules.Include(r => r.Lines)
            .Where(r => r.CompanyId == _companyId)
            .ToListAsync();

        rules.SelectMany(r => r.Lines).Should().OnlyContain(l => accountIds.Contains(l.AccountId));
    }

    [Fact]
    public async Task Ejecutar_dos_veces_no_duplica_posting_rules()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        (await verifyDb.PostingRules.CountAsync(r => r.CompanyId == _companyId)).Should().Be(6);
    }

    [Fact]
    public async Task No_toca_una_posting_rule_ya_editada_por_el_admin()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var rule = await db.PostingRules.SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Sales" && r.FactType == "InvoiceIssued"
            );
            rule.Disable(_actorId);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb
            .PostingRules.Where(r =>
                r.CompanyId == _companyId && r.SourceModule == "Sales" && r.FactType == "InvoiceIssued"
            )
            .ToListAsync();
        rules.Should()
            .ContainSingle(because: "la regla ya existe (aunque deshabilitada) — el seed nunca re-crea ni reactiva una regla existente")
            .Which.IsActive.Should()
            .BeFalse();
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
