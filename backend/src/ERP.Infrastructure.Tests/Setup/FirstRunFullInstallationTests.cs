using System.Data.Common;
using ERP.Application;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Setup;
using ERP.Application.Setup.CreateInitialAdmin;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Global;
using ERP.Infrastructure.Setup;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Setup;

/// <summary>Full real DI/EF/provisioning/handler; only the host lifetime is a test implementation.</summary>
[Collection("FirstRunPostgreSql")]
[Trait("Category", "PostgreSql")]
public sealed class FirstRunFullInstallationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("first_run_full_isolated")
        .WithUsername("synthetic_test_owner").WithPassword("SyntheticContainer1!").Build();
    private readonly Dictionary<string, string> _tables = new();
    private readonly Dictionary<string, string> _columns = new();
    private string ConnectionString => new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        { Pooling = false }.ConnectionString;

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Baseline_exclusive_owner_full_real_handler_atomic_commit_and_restart()
    {
        using var output = new StringWriter();
        var originalOutput = Console.Out;
        Console.SetOut(output); // Never expose test tokens, even on a failed assertion.
        try
        {
            var trace = new TransactionTrace(this);
            await using var owner = Provider(trace);
            await using (var scope = owner.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                await db.Database.MigrateAsync();
                var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
                // Baseline V1 primero; las migraciones forward (DATABASE.md) se aplican después.
                Assert.True(migrations[0].EndsWith("_InitialProductionBaseline", StringComparison.Ordinal));
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                foreach (var name in new[] { "IdentityUser", "Tenant", "Company", "GlobalUserRole",
                    "CompanyUserMembership", "CompanyUserBranch", "Account", "PostingRule", "Branch" })
                {
                    var entity = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == name);
                    using var quoting = new NpgsqlCommandBuilder();
                    _tables[name] = (entity.GetSchema() is { } schema ? quoting.QuoteIdentifier(schema) + "." : "")
                        + quoting.QuoteIdentifier(entity.GetTableName()!);
                    var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                    foreach (var property in entity.GetProperties())
                        if (property.GetColumnName(store) is { } column)
                            _columns[name + "." + property.Name] = quoting.QuoteIdentifier(column);
                }
                await scope.ServiceProvider.GetRequiredService<IGlobalBootstrapOrchestrator>().RunAsync();
            }
            Assert.True((await Snapshot()).All(value => value == 0));

            await Startup(owner);
            var tokenLines = output.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Length == SetupTokenCrypto.RawTokenByteLength * 2 && line.All(Uri.IsHexDigit)).ToArray();
            Assert.True(tokenLines.Length == 1);
            var syntheticToken = tokenLines[0];
            output.GetStringBuilder().Clear();
            await using (var scope = owner.CreateAsyncScope())
            {
                var state = await scope.ServiceProvider.GetRequiredService<ISystemSetupRepository>().GetAsync();
                Assert.NotNull(state);
                Assert.False(state.IsInitialized);
                Assert.True(state.HasActiveSetupToken(DateTime.UtcNow));
                Assert.True(SetupTokenCrypto.Matches(syntheticToken, state.SetupTokenHash!));
                Assert.True(state.SetupTokenHash != syntheticToken);
            }
            Assert.False(await CanAcquireLock());

            // Independent service provider = independent API ownership and scopes, same isolated DB.
            await using (var second = Provider())
                await Assert.ThrowsAsync<InvalidOperationException>(() => Startup(second));
            Assert.True(output.GetStringBuilder().Length == 0);

            trace.Active = true;
            await using (var scope = owner.CreateAsyncScope())
            {
                var handler = scope.ServiceProvider.GetRequiredService<
                    IRequestHandler<CreateInitialAdminCommand, Result<string>>>();
                Assert.IsType<CreateInitialAdminHandler>(handler);
                var result = await handler.Handle(new CreateInitialAdminCommand(
                    "synthetic.first.owner", "Synthetic", "Owner", "first.owner@example.invalid",
                    "SyntheticInstall1!", syntheticToken), default);
                Assert.True(result.IsSuccess); // No command/token/error payload in failure output.
            }
            trace.Active = false;
            Assert.Equal(1, trace.Commits);
            Assert.True(trace.Writes > 1); // Includes real provisioning's intermediate flushes.
            Assert.True(trace.SawAtomicVisibility);
            Assert.True((await Snapshot()).All(value => value == 1));
            Assert.True(await CanAcquireLock()); // Only released after successful commit.

            await using (var scope = owner.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                var state = await scope.ServiceProvider.GetRequiredService<ISystemSetupRepository>().GetAsync();
                Assert.True(state!.IsInitialized);
                Assert.Null(state.SetupTokenHash);
                Assert.Null(state.SetupTokenExpiryUtc);
                var user = await db.IdentityUsers.SingleAsync();
                Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>()
                    .VerifyPassword("SyntheticInstall1!", user.PasswordHash));
            }
            Assert.True(await HasLinkedAdminRolesAndBootstrap());

            // A new API lifetime after completion must neither acquire ownership nor print a token.
            await using (var restarted = Provider())
                await Startup(restarted);
            Assert.True(output.GetStringBuilder().Length == 0);
            Assert.True(await CanAcquireLock());
            Assert.True((await Snapshot()).All(value => value == 1));
        }
        finally
        {
            output.GetStringBuilder().Clear();
            Console.SetOut(originalOutput);
        }
    }

    private ServiceProvider Provider(TransactionTrace? trace = null)
    {
        // No appsettings, environment variables, user-secrets, launchSettings or host endpoints.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["InstallData:Enabled"] = "false",
            ["Snowflake:WorkerId"] = "17",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(logging => logging.ClearProviders());
        services.AddDistributedMemoryCache();
        services.AddSingleton<IHostApplicationLifetime, TestLifetime>();
        services.AddInfrastructure(config);
        services.AddApplication();
        if (trace is not null)
            services.ConfigureDbContext<ErpDbContext>((_, options) => options.AddInterceptors(
                trace, new WriteTrace(trace), new ConnectionTargetGuard(ConnectionString)));
        else
            services.ConfigureDbContext<ErpDbContext>((_, options) => options.AddInterceptors(
                new ConnectionTargetGuard(ConnectionString)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task Startup(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IFirstRunSetupService>().EnsureSetupTokenAsync();
    }

    // Each count/flag becomes 1 only after the complete installation is visible outside its transaction.
    private async Task<long[]> Snapshot()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var entities = new[] { "IdentityUser", "Tenant", "Company", "GlobalUserRole", "CompanyUserMembership", "CompanyUserBranch" };
        var columns = entities.Select(name => $"(SELECT count(*) FROM {_tables[name]})");
        await using var command = new NpgsqlCommand("SELECT " + string.Join(",", columns)
            + ", COALESCE((SELECT is_initialized::int::bigint FROM system_setup_state WHERE id=1), 0)", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return Enumerable.Range(0, entities.Length + 1).Select(reader.GetInt64).ToArray();
    }

    private async Task<bool> CanAcquireLock()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(1514686021, 1)", connection);
        return Equals(await command.ExecuteScalarAsync(), true);
    }

    private async Task<bool> HasLinkedAdminRolesAndBootstrap()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            SELECT EXISTS (
                SELECT 1 FROM {_tables["IdentityUser"]} u
                JOIN {_tables["GlobalUserRole"]} r ON r.{_columns["GlobalUserRole.UserId"]}=u.{_columns["IdentityUser.Id"]}
                JOIN {_tables["CompanyUserMembership"]} m ON m.{_columns["CompanyUserMembership.IdentityUserId"]}=u.{_columns["IdentityUser.Id"]}
                JOIN {_tables["Company"]} c ON c.{_columns["Company.Id"]}=m.{_columns["CompanyUserMembership.CompanyId"]}
                JOIN {_tables["Tenant"]} t ON t.{_columns["Tenant.Id"]}=c.{_columns["Company.TenantId"]}
                WHERE u.{_columns["IdentityUser.Username"]}='synthetic.first.owner'
                AND r.{_columns["GlobalUserRole.Role"]}='Admin' AND r.{_columns["GlobalUserRole.IsActive"]}
                AND m.{_columns["CompanyUserMembership.Role"]}='Admin')
                AND (SELECT count(*) FROM {_tables["Account"]}) > 0
                AND (SELECT count(*) FROM {_tables["PostingRule"]}) > 0
                AND (SELECT count(*) FROM {_tables["Branch"]}) > 0
            """, connection);
        return Equals(await command.ExecuteScalarAsync(), true);
    }

    private sealed class TransactionTrace(FirstRunFullInstallationTests test) : DbTransactionInterceptor
    {
        public bool Active { get; set; }
        public int Commits { get; private set; }
        public int Writes { get; private set; }
        public bool SawAtomicVisibility { get; private set; }
        private DbTransaction? _transaction;
        public void Record(DbCommand command)
        {
            if (!Active) return;
            Assert.NotNull(command.Transaction);
            _transaction ??= command.Transaction;
            Assert.True(ReferenceEquals(_transaction, command.Transaction));
            if (command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                || command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)) Writes++;
        }
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Active)
            {
                Assert.True(ReferenceEquals(_transaction, transaction));
                Assert.True((await test.Snapshot()).All(value => value == 0));
                Assert.False(await test.CanAcquireLock());
            }
            return result;
        }
        public override async Task TransactionCommittedAsync(DbTransaction transaction,
            TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Active)
            {
                Commits++;
                Assert.True((await test.Snapshot()).All(value => value == 1));
                Assert.False(await test.CanAcquireLock());
                SawAtomicVisibility = true;
            }
        }
    }

    private sealed class WriteTrace(TransactionTrace trace) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { trace.Record(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { trace.Record(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { trace.Record(command); return ValueTask.FromResult(result); }
    }

    private sealed class ConnectionTargetGuard(string expected) : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            var isolated = new NpgsqlConnectionStringBuilder(expected);
            Assert.True(actual.Host == isolated.Host && actual.Port == isolated.Port
                && actual.Database == "first_run_full_isolated");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopping.Token;
        public void StopApplication() => _stopping.Cancel();
    }
}
