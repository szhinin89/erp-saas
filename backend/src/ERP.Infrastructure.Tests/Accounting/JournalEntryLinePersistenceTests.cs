using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para la persistencia EF Core de
/// <see cref="JournalEntryLine"/> (Fase 3.5.4 — modelo de dominio aprobado en Fase 3.5.3). Solo
/// verifica el mapeo/las relaciones — no ejercita JournalFactory/JournalValidator/PostingPipeline/
/// PostingEngine, que siguen sin consumir estas líneas. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class JournalEntryLinePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_journal_entry_line_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _accountingPeriodId;
    private Guid _createdBy;
    private Guid _debitAccountId;
    private Guid _creditAccountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );
        var period = AccountingPeriod.Create(
            tenant.Id,
            company.Id,
            2026,
            7,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            _createdBy
        );
        var debitAccount = Account.Create(
            tenant.Id,
            company.Id,
            AccountCode.Create("1.1.01"),
            "Caja",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var creditAccount = Account.Create(
            tenant.Id,
            company.Id,
            AccountCode.Create("4.1.01"),
            "Ventas",
            null,
            AccountType.Income,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.AccountingPeriods.Add(period);
        db.Accounts.AddRange(debitAccount, creditAccount);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _accountingPeriodId = period.Id;
        _debitAccountId = debitAccount.Id;
        _creditAccountId = creditAccount.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private JournalEntry BuildBalancedEntry()
    {
        var entry = JournalEntry.Create(
            _tenantId,
            _companyId,
            new DateOnly(2026, 7, 25),
            _accountingPeriodId,
            2026,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento test",
            _createdBy
        );

        entry.AddLine(_debitAccountId, "Débito", 100m, 0m);
        entry.AddLine(_creditAccountId, "Crédito", 0m, 100m);
        return entry;
    }

    [Fact]
    public async Task Guardar_JournalEntry_con_lineas_persiste_las_lineas()
    {
        var entry = BuildBalancedEntry();

        await using var db = CreateContext();
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntryLines.CountAsync(x => x.JournalEntryId == entry.Id);
        count.Should().Be(2);
    }

    [Fact]
    public async Task Recuperar_JournalEntry_incluye_las_lineas_via_navegacion()
    {
        var entry = BuildBalancedEntry();

        await using var db = CreateContext();
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.Id == entry.Id);

        loaded.Lines.Should().HaveCount(2);
        loaded.Lines.Should().Contain(l => l.AccountId == _debitAccountId && l.Debit == 100m);
        loaded.Lines.Should().Contain(l => l.AccountId == _creditAccountId && l.Credit == 100m);
        loaded.EnsureBalanced(); // no debe lanzar — Σ Debit == Σ Credit tras recargar desde BD
    }

    [Fact]
    public async Task Insertar_linea_con_AccountId_inexistente_viola_integridad_referencial()
    {
        var entry = JournalEntry.Create(
            _tenantId,
            _companyId,
            new DateOnly(2026, 7, 25),
            _accountingPeriodId,
            2026,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento con cuenta inexistente",
            _createdBy
        );
        entry.AddLine(Guid.NewGuid(), "Cuenta inexistente", 100m, 0m);

        await using var db = CreateContext();
        db.JournalEntries.Add(entry);
        var act = async () => await db.SaveChangesAsync();

        await act.Should()
            .ThrowAsync<DbUpdateException>(
                because: "JournalEntryLine.AccountId tiene FK real a accounts (Restrict) — a diferencia de PostingRuleLine"
            );
    }

    [Fact]
    public async Task Eliminar_JournalEntry_elimina_sus_lineas_en_cascada()
    {
        var entry = BuildBalancedEntry();

        await using (var db = CreateContext())
        {
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var loaded = await db.JournalEntries.FirstAsync(x => x.Id == entry.Id);
            db.JournalEntries.Remove(loaded);
            await db.SaveChangesAsync();
        }

        await using var verifyDb = CreateContext();
        var remaining = await verifyDb.JournalEntryLines.CountAsync(x =>
            x.JournalEntryId == entry.Id
        );
        remaining
            .Should()
            .Be(0, because: "ON DELETE CASCADE elimina las líneas junto con su asiento padre");
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
