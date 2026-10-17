using ERP.Application.Common;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ACCOUNTING-CONTINUITY-04 (PostgreSQL 16 real) — el Down de la migración vuelve a NOT NULL solo si
/// ningún reembolso depende de la cuenta opcional; con datos incompatibles aborta explícitamente,
/// sin inventar cuentas ni borrar filas. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AccountingContinuity04MigrationTests : IAsyncLifetime
{
    private const string Previous = "20261016213213_AccountingContinuity02";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_accounting_continuity_04_migration")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Context();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Down_sin_reembolsos_sin_cuenta_restaura_NOT_NULL_sin_default()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Previous);

        var columns = await ColumnsAsync(db);
        columns.Should().BeEquivalentTo(new[]
        {
            ("accounting_account_code_snapshot", "NO", (string?)null),
            ("accounting_account_id", "NO", (string?)null),
        });

        await db.GetService<IMigrator>().MigrateAsync();
        (await ColumnsAsync(db)).Should().OnlyContain(c => c.Nullable == "YES");
    }

    [Fact]
    public async Task Down_con_reembolso_sin_cuenta_aborta_explicitamente_y_conserva_la_fila()
    {
        await using var db = Context();
        var id = Guid.NewGuid();
        // Fila mínima sin cuenta; solo el test omite las FK de la sesión para no sembrar su grafo.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET session_replication_role = replica;
            INSERT INTO supplier_credit_refund_transactions
                (id, tenant_id, company_id, supplier_id, supplier_credit_id, supplier_credit_movement_id,
                 transaction_type_code, cash_register_id, payment_method_code, amount, currency_code,
                 effective_date, destination_code_snapshot, destination_name_snapshot, destination_type_snapshot,
                 client_request_id, payload_hash, created_by_user_id, created_at_utc)
            VALUES ({id}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()},
                 1, {Guid.NewGuid()}, 'EFE', 10, 'USD', DATE '2026-10-16', 'C1', 'Caja 1', 'CashRegister',
                 {Guid.NewGuid()}, 'h', {Guid.NewGuid()}, now());
            SET session_replication_role = origin;
            """);

        var down = () => db.GetService<IMigrator>().MigrateAsync(Previous);

        (await down.Should().ThrowAsync<PostgresException>())
            .Which.MessageText.Should().Contain("AccountingContinuity04 Down abortado");
        await using var verify = Context();
        (await verify.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM supplier_credit_refund_transactions WHERE id = {id}").SingleAsync())
            .Should().Be(1);
        (await ColumnsAsync(verify)).Should().OnlyContain(c => c.Nullable == "YES", "el Down se revirtió completo");
    }

    private static async Task<List<(string Name, string Nullable, string? Default)>> ColumnsAsync(ErpDbContext db)
    {
        var rows = await db.Database.SqlQuery<ColumnRow>($"""
            SELECT column_name AS "Name", is_nullable AS "Nullable", column_default AS "Default"
            FROM information_schema.columns
            WHERE table_name = 'supplier_credit_refund_transactions'
              AND column_name IN ('accounting_account_id', 'accounting_account_code_snapshot')
            ORDER BY column_name
            """).ToListAsync();
        return rows.Select(r => (r.Name, r.Nullable, r.Default)).ToList();
    }

    private sealed record ColumnRow(string Name, string Nullable, string? Default);

    private ErpDbContext Context() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options,
            new NoTenant(),
            new NoPublisher(),
            new NoCompany()
        );

    private sealed class NoTenant : ICurrentTenant
    {
        public Guid TenantId => Guid.Empty;
        public string? Slug => null;
    }

    private sealed class NoCompany : ICurrentCompany
    {
        public Guid CompanyId => Guid.Empty;
        public bool IsAuthenticated => false;
        public bool HasCompanyContext => false;
    }

    private sealed class NoPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
