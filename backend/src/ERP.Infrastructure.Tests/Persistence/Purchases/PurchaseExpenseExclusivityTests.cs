using ERP.Application.Common;
using ERP.Infrastructure.Migrations;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Purchases;

// Apply the actual migration to the minimal pre-existing tables needed by its constraints.
// Full baseline compatibility is also exercised by PurchaseReceptionDocumentRepositoryTests.
public sealed class PurchaseExpenseExclusivityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var connection = await Open();
        await new NpgsqlCommand("""
            CREATE TABLE purchase_reception_documents (id uuid PRIMARY KEY);
            CREATE TABLE purchase_invoices (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, access_key varchar(49));
            CREATE TABLE expense_documents (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL);
            """, connection).ExecuteNonQueryAsync();
        await Apply(new ExpensesFromPurchaseReception().UpOperations, connection);
    }

    private async Task Apply(IReadOnlyList<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> operations, NpgsqlConnection connection)
    {
        await using var db = new ErpDbContext(new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options,
            Mock.Of<ICurrentTenant>(), Mock.Of<IPublisher>(), Mock.Of<ICurrentCompany>());
        foreach (var sql in db.GetService<IMigrationsSqlGenerator>().Generate(operations))
            await new NpgsqlCommand(sql.CommandText, connection).ExecuteNonQueryAsync();
    }

    private async Task<NpgsqlConnection> Open()
    {
        var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    private static NpgsqlCommand Insert(NpgsqlConnection connection, bool expense, Guid tenant, string? key)
    {
        var table = expense ? "expense_documents" : "purchase_invoices";
        var command = new NpgsqlCommand($"INSERT INTO {table} (tenant_id, access_key) VALUES (@tenant, @key)", connection);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("key", NpgsqlTypes.NpgsqlDbType.Varchar, (object?)key ?? DBNull.Value);
        return command;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_purchase_and_expense_allow_only_one(bool expenseFirst)
    {
        await using var first = await Open();
        await using var second = await Open();
        await using var observer = await Open();
        var tenant = Guid.NewGuid();
        var key = new string('1', 49);
        await using var tx = await first.BeginTransactionAsync();
        await Insert(first, expenseFirst, tenant, key).ExecuteNonQueryAsync();
        var pending = Insert(second, !expenseFirst, tenant, key).ExecuteNonQueryAsync();
        // Wait until PostgreSQL confirms that the second writer is blocked on our lock.
        var blocked = false;
        for (var attempt = 0; attempt < 100 && !pending.IsCompleted; attempt++)
        {
            await using var probe = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = @pid AND wait_event = 'advisory')", observer);
            probe.Parameters.AddWithValue("pid", second.ProcessID);
            blocked = (bool)(await probe.ExecuteScalarAsync())!;
            if (blocked) break;
            await Task.Delay(20);
        }
        blocked.Should().BeTrue();
        await tx.CommitAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(async () => await pending);
        error.SqlState.Should().Be("23505");
        error.ConstraintName.Should().Be("uq_purchase_expense_access_key");
        var count = await new NpgsqlCommand("SELECT (SELECT count(*) FROM expense_documents) + (SELECT count(*) FROM purchase_invoices)", observer).ExecuteScalarAsync();
        count.Should().Be(1L);
    }

    [Fact]
    public async Task Migration_preserves_manual_documents_tenant_scope_and_reception_uniqueness()
    {
        await using var connection = await Open();
        var tenant = Guid.NewGuid();
        await Insert(connection, true, tenant, null).ExecuteNonQueryAsync();
        await Insert(connection, false, tenant, null).ExecuteNonQueryAsync();
        await Insert(connection, true, tenant, "key").ExecuteNonQueryAsync();
        await Insert(connection, false, Guid.NewGuid(), "key").ExecuteNonQueryAsync();
        var duplicateKey = await Assert.ThrowsAsync<PostgresException>(async () =>
            await Insert(connection, true, tenant, "key").ExecuteNonQueryAsync());
        duplicateKey.ConstraintName.Should().Be("uq_expense_documents_tenant_access_key");
        var reception = Guid.NewGuid();
        await using var receptionCommand = new NpgsqlCommand("INSERT INTO purchase_reception_documents VALUES (@id)", connection);
        receptionCommand.Parameters.AddWithValue("id", reception);
        await receptionCommand.ExecuteNonQueryAsync();
        await using var expense = new NpgsqlCommand("INSERT INTO expense_documents (tenant_id, reception_document_id) VALUES (@tenant, @id)", connection);
        expense.Parameters.AddWithValue("tenant", tenant);
        expense.Parameters.AddWithValue("id", reception);
        await expense.ExecuteNonQueryAsync();
        var duplicateId = await Assert.ThrowsAsync<PostgresException>(async () => await expense.ExecuteNonQueryAsync());
        duplicateId.ConstraintName.Should().Be("uq_expense_documents_tenant_reception_document_id");
        await Apply(new ExpensesFromPurchaseReception().DownOperations, connection);
        var columns = await new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_name = 'expense_documents' AND column_name IN ('access_key', 'reception_document_id')", connection).ExecuteScalarAsync();
        columns.Should().Be(0L);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
