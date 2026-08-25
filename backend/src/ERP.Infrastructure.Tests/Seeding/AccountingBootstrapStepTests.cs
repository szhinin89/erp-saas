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
    public async Task Primera_ejecucion_crea_13_cuentas_y_un_periodo_anual_abierto()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var accounts = await db.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        accounts.Should().HaveCount(13);
        accounts.Should().OnlyContain(a => a.IsActive && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "1.1.01.001" && a.Name == "Caja General");
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
        (await verifyDb.Accounts.CountAsync(a => a.CompanyId == _companyId)).Should().Be(13);
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
        accounts.Should().HaveCount(13);
        accounts.Should().Contain(a => a.Code.Value == "6.1.01.001" && a.Name == "Gastos administrativos");
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
