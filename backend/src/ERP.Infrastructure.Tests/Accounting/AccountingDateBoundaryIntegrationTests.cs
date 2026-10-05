using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Tests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ZH-ACCOUNTING-DATE-BOUNDARY-01 — el día contable de un hecho sin fecha propia (autorización de
/// devolución de compra/NC, aplicación y reversa de crédito de proveedor, reversa de cobro) es el
/// "hoy" de la EMPRESA (<see cref="CompanyClock"/>, Company.Timezone = America/Guayaquil, ADR-034),
/// nunca el día UTC. Reproduce el camino de los traductores —
/// <c>ICompanyClock.TodayAsync</c> → <see cref="PostingFact.EntryDate"/> → <see cref="PostingEngine"/>
/// → período que contiene esa fecha — con el reloj fijado (sin depender de la hora real), contra
/// PostgreSQL real y verificando desde otro DbContext.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AccountingDateBoundaryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_date_boundary_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _createdBy = Guid.NewGuid();
    private Guid _tenantId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();
        var tenant = Tenant.Create("Date Boundary", $"db-{Guid.NewGuid():N}"[..16], _createdBy);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    private sealed record Scenario(Guid CompanyId, Dictionary<(int Year, int Month), Guid> Periods);

    /// <summary>Empresa propia (zona por defecto America/Guayaquil), regla y los períodos pedidos.</summary>
    private async Task<Scenario> SeedAsync(params (int Year, int Month, bool Closed)[] periods)
    {
        await using var db = CreateContext(Guid.Empty);
        var company = Company.CreateManaged(
            _tenantId,
            $"17{Guid.NewGuid():N}"[..13],
            "Frontera S.A.",
            createdBy: _createdBy
        );
        company.Timezone.Should().Be("America/Guayaquil");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        Account NewAccount(string prefix, AccountType type, AccountNature nature) =>
            Account.Create(
                _tenantId,
                company.Id,
                AccountCode.Create($"{prefix}.{Guid.NewGuid():N}"[..8]),
                prefix,
                null,
                type,
                nature,
                allowsPosting: true,
                createdBy: _createdBy
            );
        var debit = NewAccount("1.1", AccountType.Asset, AccountNature.Debit);
        var credit = NewAccount("4.1", AccountType.Income, AccountNature.Credit);
        var vat = NewAccount("2.1", AccountType.Liability, AccountNature.Credit);
        db.Accounts.AddRange(debit, credit, vat);
        var rule = PostingRule.Create(
            _tenantId,
            company.Id,
            "Test",
            "BoundaryFact",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(debit.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(credit.Id, AccountNature.Credit, PostingAmountKind.Subtotal);
        rule.AddLine(vat.Id, AccountNature.Credit, PostingAmountKind.TaxVat);
        db.PostingRules.Add(rule);

        var ids = new Dictionary<(int, int), Guid>();
        foreach (var (year, month, closed) in periods)
        {
            var period = AccountingPeriod.Create(
                _tenantId,
                company.Id,
                year,
                month,
                new DateOnly(year, month, 1),
                new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
                _createdBy
            );
            if (closed)
                period.Close(_createdBy, new JournalEntryClosureReadiness(false, false, false));
            db.AccountingPeriods.Add(period);
            ids[(year, month)] = period.Id;
        }
        await db.SaveChangesAsync();
        return new Scenario(company.Id, ids);
    }

    /// <summary>Mismo camino que un traductor sin fecha de negocio propia, con el reloj fijado.</summary>
    private async Task<(DateOnly EntryDate, Result<PostingOutcomeDto> Result)> PostWithClockAsync(
        Guid companyId,
        DateTimeOffset utcNow
    )
    {
        await using var db = CreateContext(companyId);
        var entryDate = await new CompanyClock(db, new FixedTimeProvider(utcNow)).TodayAsync(
            companyId,
            _tenantId
        );
        var fact = new PostingFact(
            _tenantId,
            companyId,
            "Test",
            "BoundaryFact",
            Guid.NewGuid(),
            entryDate,
            100m,
            15m,
            0m,
            0m,
            115m
        );
        var engine = new PostingEngine(
            new JournalEntryRepository(db),
            new PostingRuleRepository(db),
            new AccountingPeriodRepository(db),
            new JournalEntrySequenceRepository(db),
            new AccountRepository(db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PostingEngine>.Instance
        );
        await using var tx = await db.Database.BeginTransactionAsync();
        var result = await engine.PostAsync(fact);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (entryDate, result);
    }

    public static TheoryData<string, DateTimeOffset, DateOnly, int, int> Boundaries =>
        new()
        {
            {
                "día normal",
                new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero),
                new DateOnly(2026, 9, 15),
                2026,
                10
            },
            {
                "UTC ya 1-oct, empresa 30-sep",
                new DateTimeOffset(2026, 10, 1, 0, 30, 0, TimeSpan.Zero),
                new DateOnly(2026, 9, 30),
                2026,
                10
            },
            {
                "1-oct también en la empresa",
                new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.Zero),
                new DateOnly(2026, 10, 1),
                2026,
                9
            },
            {
                "UTC ya 1-ene, empresa 31-dic",
                new DateTimeOffset(2027, 1, 1, 4, 59, 59, TimeSpan.Zero),
                new DateOnly(2026, 12, 31),
                2027,
                1
            },
            {
                "1-ene también en la empresa",
                new DateTimeOffset(2027, 1, 1, 5, 0, 0, TimeSpan.Zero),
                new DateOnly(2027, 1, 1),
                2026,
                12
            },
        };

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task El_asiento_cae_en_el_periodo_del_dia_de_la_empresa(
        string scenario,
        DateTimeOffset utcNow,
        DateOnly companyDate,
        int otherYear,
        int otherMonth
    )
    {
        var s = await SeedAsync(
            (companyDate.Year, companyDate.Month, false),
            (otherYear, otherMonth, false)
        );

        var (entryDate, result) = await PostWithClockAsync(s.CompanyId, utcNow);

        entryDate.Should().Be(companyDate, scenario);
        result.IsSuccess.Should().BeTrue($"{scenario}: {result.Code} {result.Error}");
        await using var verify = CreateContext(s.CompanyId);
        var entry = await verify
            .JournalEntries.IgnoreQueryFilters()
            .Include(e => e.Lines)
            .SingleAsync(e => e.Id == result.Value!.JournalEntryId);
        entry.EntryDate.Should().Be(companyDate);
        entry
            .AccountingPeriodId.Should()
            .Be(
                s.Periods[(companyDate.Year, companyDate.Month)],
                "período del día de la empresa, no del día UTC"
            );
        entry.FiscalYear.Should().Be(companyDate.Year);
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.Lines.Sum(l => l.Debit).Should().Be(115m);
        entry.Lines.Sum(l => l.Credit).Should().Be(115m);
    }

    [Fact]
    public async Task Periodo_del_dia_de_la_empresa_cerrado_rechaza_con_PERIOD_NOT_OPEN_sin_desviarse_al_mes_UTC()
    {
        var s = await SeedAsync((2026, 9, true), (2026, 10, false));

        var (_, result) = await PostWithClockAsync(s.CompanyId, AccountingDateBoundary.UtcInstant);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        await using var verify = CreateContext(s.CompanyId);
        (
            await verify
                .JournalEntries.IgnoreQueryFilters()
                .CountAsync(e => e.CompanyId == s.CompanyId)
        )
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task Periodo_del_dia_de_la_empresa_inexistente_rechaza_con_PERIOD_NOT_OPEN()
    {
        var s = await SeedAsync((2026, 10, false));

        var (entryDate, result) = await PostWithClockAsync(
            s.CompanyId,
            AccountingDateBoundary.UtcInstant
        );

        entryDate.Should().Be(AccountingDateBoundary.CompanyToday);
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        await using var verify = CreateContext(s.CompanyId);
        (
            await verify
                .JournalEntries.IgnoreQueryFilters()
                .CountAsync(e => e.CompanyId == s.CompanyId)
        )
            .Should()
            .Be(0);
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
