using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
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
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el cierre real de período
/// (Fase 5.5, ADR-026 §6.1/§9). Cubre lo que un mock no puede: que
/// IJournalEntryRepository.GetClosureReadinessAsync resuelva correctamente las 3 precondiciones
/// (EXISTS, sin materializar entidades) contra datos reales de journal_entries. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CloseAccountingPeriodIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_close_period_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
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
        var debitAccount = Account.Create(
            tenant.Id,
            company.Id,
            ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.01"),
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
            ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("4.1.01"),
            "Ventas",
            null,
            AccountType.Income,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Accounts.AddRange(debitAccount, creditAccount);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
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

    private async Task<AccountingPeriod> SeedPeriodAsync()
    {
        await using var db = CreateContext();
        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            2026,
            7,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            _createdBy
        );
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period;
    }

    private async Task<JournalEntry> SeedPostedEntryAsync(AccountingPeriod period)
    {
        await using var db = CreateContext();
        var entryNumber = await new JournalEntrySequenceRepository(db).ReserveNextNumberAsync(
            _tenantId,
            _companyId,
            period.FiscalYear,
            CancellationToken.None
        );

        var entry = JournalEntry.Create(
            _tenantId,
            _companyId,
            new DateOnly(2026, 7, 15),
            period.Id,
            period.FiscalYear,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento test",
            _createdBy
        );
        entry.AddLine(_debitAccountId, null, 100m, 0m);
        entry.AddLine(_creditAccountId, null, 0m, 100m);
        entry.Post(_createdBy, entryNumber);

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private async Task<JournalEntry> SeedDraftEntryAsync(AccountingPeriod period)
    {
        await using var db = CreateContext();
        var entry = JournalEntry.Create(
            _tenantId,
            _companyId,
            new DateOnly(2026, 7, 15),
            period.Id,
            period.FiscalYear,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento sin publicar",
            _createdBy
        );

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static CloseAccountingPeriodHandler BuildHandler(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid userId
    ) =>
        new(
            new AccountingPeriodRepository(db),
            new JournalEntryRepository(db),
            new FixedCurrentTenant(tenantId),
            new FixedCurrentCompany(companyId),
            new FixedCurrentUser(userId)
        );

    [Fact]
    public async Task Cierre_correcto_cuando_todos_los_asientos_estan_Posted()
    {
        var period = await SeedPeriodAsync();
        await SeedPostedEntryAsync(period);
        await SeedPostedEntryAsync(period);

        await using var db = CreateContext();
        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb.AccountingPeriods.FirstAsync(x => x.Id == period.Id);
        loaded.Status.Should().Be(PeriodStatus.Closed);
        loaded.ClosedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Cierre_falla_si_existe_un_asiento_Draft_y_el_periodo_permanece_Open()
    {
        var period = await SeedPeriodAsync();
        await SeedPostedEntryAsync(period);
        await SeedDraftEntryAsync(period);

        await using var db = CreateContext();
        var result = await BuildHandler(db, _tenantId, _companyId, _createdBy)
            .Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sin publicar");

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb.AccountingPeriods.FirstAsync(x => x.Id == period.Id);
        loaded.Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task Cierre_correcto_con_un_asiento_reversado_completo()
    {
        var period = await SeedPeriodAsync();
        var original = await SeedPostedEntryAsync(period);

        await using (var db = CreateContext())
        {
            var entryNumber = await new JournalEntrySequenceRepository(db).ReserveNextNumberAsync(
                _tenantId,
                _companyId,
                period.FiscalYear,
                CancellationToken.None
            );
            var tracked = await db
                .JournalEntries.Include(x => x.Lines)
                .FirstAsync(x => x.Id == original.Id);
            var reversal = tracked.Reverse(_createdBy, entryNumber, "Ajuste de prueba");
            db.JournalEntries.Add(reversal);
            await db.SaveChangesAsync();
        }

        await using var closeDb = CreateContext();
        var result = await BuildHandler(closeDb, _tenantId, _companyId, _createdBy)
            .Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result
            .IsSuccess.Should()
            .BeTrue(
                because: "un asiento Reversed con su reverso Posted y numerado es un cierre completo"
            );

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb.AccountingPeriods.FirstAsync(x => x.Id == period.Id);
        loaded.Status.Should().Be(PeriodStatus.Closed);
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

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
