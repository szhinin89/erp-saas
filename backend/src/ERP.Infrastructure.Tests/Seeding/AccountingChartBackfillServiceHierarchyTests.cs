using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Services;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 3: backfill de ParentAccountId para companies
/// existentes con datos legacy (padre null, padre desalineado del código, códigos que saltan
/// niveles).
/// </summary>
public sealed class AccountingChartBackfillServiceHierarchyTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private ErpDbContext NewDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private AccountingChartBackfillService NewService(ErpDbContext db) =>
        new(
            db,
            new FakeHostEnvironment(isProduction: false),
            new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance),
            NullLogger<AccountingChartBackfillService>.Instance
        );

    private Account AddAccount(
        ErpDbContext db,
        string code,
        Guid? parentId,
        AccountType type = AccountType.Asset,
        AccountNature nature = AccountNature.Debit,
        bool allowsPosting = true
    )
    {
        var account = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create(code),
            $"Cuenta {code}",
            parentId,
            type,
            nature,
            allowsPosting,
            _actorId
        );
        db.Accounts.Add(account);
        return account;
    }

    [Fact]
    public async Task Corrige_padre_null_legacy_de_una_cuenta_con_codigo_compuesto()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid rootId,
            midId,
            leafId;

        await using (var db = NewDbContext(dbName))
        {
            var root = AddAccount(db, "1", null, allowsPosting: false);
            var mid = AddAccount(db, "1.1", root.Id, allowsPosting: false);
            // Legacy: hoja con ParentAccountId null aunque su código implica padre "1.1".
            var leaf = AddAccount(db, "1.1.01", null, allowsPosting: true);
            await db.SaveChangesAsync();
            (rootId, midId, leafId) = (root.Id, mid.Id, leaf.Id);
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            var result = await service.BackfillHierarchyAsync(_tenantId, _companyId, _actorId);
            result.FixedParentCount.Should().Be(1);
            result.UnresolvedParentCount.Should().Be(0);
        }

        await using var verifyDb = NewDbContext(dbName);
        var leafAfter = await verifyDb.Accounts.SingleAsync(a => a.Id == leafId);
        leafAfter.ParentAccountId.Should().Be(midId);
    }

    [Fact]
    public async Task Corrige_padre_que_salta_nivel_intermedio()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid midId,
            leafId;

        await using (var db = NewDbContext(dbName))
        {
            var root = AddAccount(db, "5", null, type: AccountType.Cost, nature: AccountNature.Debit, allowsPosting: false);
            var mid = AddAccount(db, "5.1", root.Id, type: AccountType.Cost, nature: AccountNature.Debit, allowsPosting: false);
            // Legacy: apunta directo a la raíz "5" en vez de al intermedio "5.1" (mismo bug que
            // tenía "5.1.02" en el blueprint original antes de ACCOUNTING-CHART-CANONICAL-HIERARCHY-01).
            var leaf = AddAccount(db, "5.1.02", root.Id, type: AccountType.Cost, nature: AccountNature.Debit, allowsPosting: false);
            await db.SaveChangesAsync();
            (midId, leafId) = (mid.Id, leaf.Id);
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            var result = await service.BackfillHierarchyAsync(_tenantId, _companyId, _actorId);
            result.FixedParentCount.Should().Be(1);
        }

        await using var verifyDb = NewDbContext(dbName);
        var leafAfter = await verifyDb.Accounts.SingleAsync(a => a.Id == leafId);
        leafAfter.ParentAccountId.Should().Be(midId);
    }

    [Fact]
    public async Task No_toca_ni_inventa_padre_para_codigo_custom_fuera_del_blueprint()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            // Cuenta custom del usuario cuyo código implica un padre "9.9" que nunca fue creado.
            AddAccount(db, "9.9.01", null, allowsPosting: true);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            var result = await service.BackfillHierarchyAsync(_tenantId, _companyId, _actorId);
            result.FixedParentCount.Should().Be(0);
            result.UnresolvedParentCount.Should().Be(1);
        }

        await using var verifyDb = NewDbContext(dbName);
        var account = await verifyDb.Accounts.SingleAsync(a => a.Code.Value == "9.9.01");
        account.ParentAccountId.Should().BeNull();
    }

    [Fact]
    public async Task Ejecutar_dos_veces_deja_el_mismo_resultado_idempotente()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            AddAccount(db, "1", null, allowsPosting: false);
            AddAccount(db, "1.1", null, allowsPosting: true); // legacy, padre null
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            var first = await service.BackfillHierarchyAsync(_tenantId, _companyId, _actorId);
            first.FixedParentCount.Should().Be(1);
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            var second = await service.BackfillHierarchyAsync(_tenantId, _companyId, _actorId);
            second.FixedParentCount.Should().Be(0);
            second.UnresolvedParentCount.Should().Be(0);
        }

        await using var verifyDb = NewDbContext(dbName);
        (await verifyDb.Accounts.CountAsync(a => a.CompanyId == _companyId)).Should().Be(2);
    }

    /// <summary>
    /// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01: cubre el comando controlado
    /// `backfill-accounting-chart-hierarchy` — diagnóstico antes, fix transaccional, diagnóstico
    /// después, y compañías sin hallazgos quedan intactas (sin transacción ni SaveChanges).
    /// </summary>
    [Fact]
    public async Task RunControlledHierarchyMaintenanceAsync_diagnostica_corrige_y_reporta_antes_despues()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid companyWithIssuesId,
            leafId,
            midId;
        Guid companyCleanId;

        await using (var db = NewDbContext(dbName))
        {
            var companyWithIssues = ERP.Domain.Modules.Company.Entities.Company.CreateFromTenant(
                _tenantId,
                "1790000000001",
                "Empresa con inconsistencias"
            );
            db.Companies.Add(companyWithIssues);
            companyWithIssuesId = companyWithIssues.Id;

            var companyClean = ERP.Domain.Modules.Company.Entities.Company.CreateFromTenant(
                _tenantId,
                "1790000000002",
                "Empresa sin inconsistencias"
            );
            db.Companies.Add(companyClean);
            companyCleanId = companyClean.Id;

            await db.SaveChangesAsync();
        }

        // Company con inconsistencias: hoja con padre null aunque su código implica padre "1.1".
        Account rootA,
            midA,
            leafA;
        await using (var db = NewDbContext(dbName))
        {
            rootA = Account.Create(
                _tenantId,
                companyWithIssuesId,
                AccountCode.Create("1"),
                "Activo",
                null,
                AccountType.Asset,
                AccountNature.Debit,
                false,
                _actorId
            );
            midA = Account.Create(
                _tenantId,
                companyWithIssuesId,
                AccountCode.Create("1.1"),
                "Activo corriente",
                rootA.Id,
                AccountType.Asset,
                AccountNature.Debit,
                false,
                _actorId
            );
            leafA = Account.Create(
                _tenantId,
                companyWithIssuesId,
                AccountCode.Create("1.1.01"),
                "Caja",
                null, // legacy: debería ser midA.Id
                AccountType.Asset,
                AccountNature.Debit,
                true,
                _actorId
            );
            db.Accounts.AddRange(rootA, midA, leafA);
            await db.SaveChangesAsync();
            (leafId, midId) = (leafA.Id, midA.Id);
        }

        // Company sin inconsistencias: jerarquía ya correcta.
        await using (var db = NewDbContext(dbName))
        {
            var rootB = Account.Create(
                _tenantId,
                companyCleanId,
                AccountCode.Create("1"),
                "Activo",
                null,
                AccountType.Asset,
                AccountNature.Debit,
                false,
                _actorId
            );
            db.Accounts.Add(rootB);
            await db.SaveChangesAsync();
        }

        AccountingHierarchyMaintenanceSummary summary;
        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            summary = await service.RunControlledHierarchyMaintenanceAsync();
        }

        summary.TotalCompanies.Should().Be(2);
        summary.CompaniesWithIssuesBefore.Should().Be(1);
        summary.CompaniesWithIssuesAfter.Should().Be(0);
        summary.TotalFixed.Should().Be(1);
        summary.TotalUnresolved.Should().Be(0);

        var companyWithIssuesResult = summary.Companies.Single(c => c.CompanyId == companyWithIssuesId);
        companyWithIssuesResult.IssuesBefore.Should().BeGreaterThan(0);
        companyWithIssuesResult.IssuesAfter.Should().Be(0);
        companyWithIssuesResult.FixedParentCount.Should().Be(1);

        var companyCleanResult = summary.Companies.Single(c => c.CompanyId == companyCleanId);
        companyCleanResult.IssuesBefore.Should().Be(0);
        companyCleanResult.FixedParentCount.Should().Be(0);

        await using var verifyDb = NewDbContext(dbName);
        var leafAfter = await verifyDb.Accounts.IgnoreQueryFilters().SingleAsync(a => a.Id == leafId);
        leafAfter.ParentAccountId.Should().Be(midId);
    }

    [Fact]
    public async Task Bootstrap_mas_backfill_de_jerarquia_deja_el_plan_sin_inconsistencias()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            // Empresa legacy: solo tenía el seed mínimo de 8 cuentas hoja con padre null,
            // como las companies creadas antes de ACCOUNTING-BASE-CHART-TEMPLATE-13.
            var legacyCodes = new[]
            {
                "1.1.01.001",
                "1.1.02.001",
                "1.1.03.001",
                "2.1.01.001",
                "3.1.01.001",
                "4.1.01.001",
                "5.1.01.001",
                "6.1.01.001",
            };
            foreach (var code in legacyCodes)
                AddAccount(db, code, null);
            await db.SaveChangesAsync();
        }

        // Mismo orden que EnsureAsync: primero re-corre AccountingBootstrapStep (crea las cuentas
        // retail/agrupadoras faltantes), luego BackfillHierarchyAsync (repara ParentAccountId de
        // las cuentas legacy que ya existían con padre null).
        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            await service.BackfillHierarchyAsync(_tenantId, _companyId, _actorId);
        }

        await using var verifyDb = NewDbContext(dbName);
        var accounts = await verifyDb.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        var report = AccountHierarchyDiagnostics.Analyze(accounts);

        report.Issues.Should().BeEmpty();
    }

    private sealed class FakeHostEnvironment(bool isProduction)
        : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isProduction
            ? Microsoft.Extensions.Hosting.Environments.Production
            : Microsoft.Extensions.Hosting.Environments.Development;
        public string ApplicationName { get; set; } = "ERP.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
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
