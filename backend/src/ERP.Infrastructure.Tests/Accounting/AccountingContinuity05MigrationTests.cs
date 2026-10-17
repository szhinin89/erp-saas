using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 (PostgreSQL 16 real vía Testcontainers) — migración de
/// <c>inventory_cost_postings</c> a la obligación común: las filas de importe cero sin asiento (cualquier
/// estado) quedan solo como evidencia legacy; las filas con importe o con asiento conservan identidad,
/// estado y asiento.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AccountingContinuity05MigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261016233910_AccountingContinuity04";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_accounting_c05_migration_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            Mock.Of<ICurrentTenant>(t => t.TenantId == Guid.Empty),
            Mock.Of<IPublisher>(),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == Guid.Empty)
        );

    [Fact]
    public async Task C05_migracion_omite_filas_cero_sin_asiento_y_conserva_las_que_tienen_efecto()
    {
        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), invoice = Guid.NewGuid(), user = Guid.NewGuid();
        Guid entry = Guid.NewGuid(), postedRow = Guid.NewGuid(), postedSource = Guid.NewGuid();
        Guid zeroPending = Guid.NewGuid(), zeroFailed = Guid.NewGuid(), zeroPosted = Guid.NewGuid();
        Guid failedRow = Guid.NewGuid(), failedSource = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            // Evidencia legacy previa a CONTINUITY-05; sin integridad referencial para aislar la prueba.
            await db.Database.ExecuteSqlRawAsync($"""
                SET session_replication_role = replica;
                INSERT INTO journal_entries (id, accounting_period_id, company_id, created_at, created_by, description,
                    entry_date, fiscal_year, source_event_id, source_event_type, source_module, status, tenant_id)
                VALUES ('{entry}', '{Guid.NewGuid()}', '{company}', now(), '{user}', 'COGS legacy',
                    '2026-07-10', 2026, '{postedSource}', 'CostOfGoodsSold', 'Sales', 2, '{tenant}');
                INSERT INTO inventory_cost_postings ("Id", "CompanyId", "InvoiceId", "SourceEventId", "FactType", "Amount",
                    "EntryDate", "Kind", "Status", "JournalEntryId", "Attempts", "TenantId", "CreatedAt", "CreatedBy")
                VALUES
                    ('{zeroPending}', '{company}', '{invoice}', '{Guid.NewGuid()}', 'CostOfGoodsSold', 0,
                        '2026-07-10', 'Coverage', 'Pending', NULL, 0, '{tenant}', now(), '{user}'),
                    ('{zeroFailed}', '{company}', '{invoice}', '{Guid.NewGuid()}', 'CostOfGoodsSoldReversed', 0,
                        '2026-07-10', 'Return', 'Failed', NULL, 1, '{tenant}', now(), '{user}'),
                    ('{zeroPosted}', '{company}', '{invoice}', '{Guid.NewGuid()}', 'CostOfGoodsSold', 0,
                        '2026-07-10', 'Coverage', 'Posted', NULL, 1, '{tenant}', now(), '{user}'),
                    ('{postedRow}', '{company}', '{invoice}', '{postedSource}', 'CostOfGoodsSold', 12.5,
                        '2026-07-10', 'Original', 'Posted', '{entry}', 1, '{tenant}', now(), '{user}'),
                    ('{failedRow}', '{company}', '{invoice}', '{failedSource}', 'CostOfGoodsSold', 3,
                        '2026-07-11', 'Coverage', 'Failed', NULL, 1, '{tenant}', now(), '{user}');
                SET session_replication_role = origin;
                """);
        }

        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        await using var verify = CreateContext();
        var migrated = await verify.AccountingPostings.IgnoreQueryFilters()
            .Where(p => p.CompanyId == company).ToListAsync();
        migrated.Select(p => p.Id).Should().BeEquivalentTo(new[] { postedRow, failedRow },
            "ninguna fila de importe cero sin asiento se migra, sea cual sea su estado");
        var failed = migrated.Single(p => p.Id == failedRow);
        failed.SourceEventId.Should().Be(failedSource);
        failed.Status.Should().Be(AccountingPostingStatus.Failed);
        failed.JournalEntryId.Should().BeNull();
        var posting = migrated.Single(p => p.Id == postedRow);
        posting.SourceModule.Should().Be("Sales");
        posting.FactType.Should().Be("CostOfGoodsSold");
        posting.SourceEventId.Should().Be(postedSource);
        posting.Status.Should().Be(AccountingPostingStatus.Posted);
        posting.JournalEntryId.Should().Be(entry);
        posting.SourceDocumentId.Should().Be(invoice);
        ERP.Application.Modules.Accounting.Continuity.AccountingPostingPayload
            .Deserialize(posting.PayloadVersion, posting.Payload)!.HistoricalCostTotal.Should().Be(12.5m);
        (await verify.Database.SqlQueryRaw<int>(
                $"SELECT count(*)::int AS \"Value\" FROM inventory_cost_postings WHERE \"Id\" IN ('{zeroPending}', '{zeroFailed}', '{zeroPosted}')")
            .SingleAsync()).Should().Be(3, "las filas cero quedan como evidencia legacy de solo lectura");
    }
}
