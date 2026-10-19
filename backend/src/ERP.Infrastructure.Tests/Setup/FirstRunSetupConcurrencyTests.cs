using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Setup;
using ERP.Application.Setup.CreateInitialAdmin;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Setup;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Setup;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Setup;

[CollectionDefinition("FirstRunPostgreSql", DisableParallelization = true)]
public sealed class FirstRunPostgreSqlCollection { }

// Only a new Testcontainer. No environment/user-secrets/host connection strings or API startup.
// The narrow test schema mirrors the existing setup mapping; no migrations, seeds or accounts.
[Collection("FirstRunPostgreSql")]
[Trait("Category", "PostgreSql")]
public sealed class FirstRunSetupConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("first_run_isolated")
        .WithUsername("test_owner").WithPassword("isolated_test_password").Build();
    private string ConnectionString => new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
    { Pooling = false }.ConnectionString;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await ExecuteAsync("""
            CREATE TABLE system_setup_state (
                id integer PRIMARY KEY, is_initialized boolean NOT NULL DEFAULT false,
                initialized_at_utc timestamptz, admin_email varchar(256),
                is_first_run boolean NOT NULL DEFAULT true,
                setup_token_hash varchar(128), setup_token_expiry_utc timestamptz);
            CREATE TABLE first_run_probe (id integer PRIMARY KEY);
            CREATE TABLE first_run_commit_probe (id integer UNIQUE DEFERRABLE INITIALLY DEFERRED);
            """);
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Concurrent_startups_with_missing_state_have_one_owner_and_one_insert()
    {
        using var owner = Coordinator();
        using var contender = Coordinator();
        await using var first = Context(owner);
        await using var second = Context(contender);
        var entered = Signal();
        var release = Signal();
        var firstStartup = Session(owner, first).RunStartupAsync(async ct =>
        {
            AssertDedicated(first);
            await IssueSyntheticState(first, ct);
            entered.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Session(contender, second)
                .RunStartupAsync(_ => throw new Exception("Contender must not emit"), default));
        }
        finally { release.SetResult(); }
        await firstStartup;
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM system_setup_state"));
    }

    [Fact]
    public async Task Existing_pending_state_is_not_replaced_by_a_second_instance()
    {
        using var owner = Coordinator();
        await StartSynthetic(owner);
        using var contender = Coordinator();
        await using var db = Context(contender);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Session(contender, db)
            .RunStartupAsync(_ => throw new Exception("Must not run"), default));
        Assert.Equal("synthetic-hash-only", await ScalarAsync("SELECT setup_token_hash FROM system_setup_state"));
    }

    [Fact]
    public async Task Process_shutdown_releases_session_and_replacement_owner_can_start()
    {
        var owner = Coordinator();
        await StartSynthetic(owner);
        owner.Dispose();
        using var replacement = Coordinator();
        await using var db = Context(replacement);
        await Session(replacement, db).RunStartupAsync(ct => IssueSyntheticState(db, ct), default);
        await using var oldDb = Context(owner);
        await Assert.ThrowsAnyAsync<Exception>(() => Session(owner, oldDb)
            .RunInstallationAsync(_ => Task.FromResult(true), default));
    }

    [Fact]
    public async Task Backend_loss_stops_owner_and_monitor_does_not_reconnect()
    {
        var lifetime = new TestLifetime();
        using var owner = Coordinator(lifetime);
        var pid = await StartSynthetic(owner);
        await owner.StartAsync(default);
        await ExecuteAsync($"SELECT pg_terminate_backend({pid})");
        await lifetime.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await using var db = Context(owner);
        await Assert.ThrowsAnyAsync<Exception>(() => Session(owner, db)
            .RunInstallationAsync(_ => Task.FromResult(true), default));
        using var replacement = Coordinator();
        await StartSynthetic(replacement);
        await owner.StopAsync(default);
    }

    [Fact]
    public async Task Connection_loss_during_transaction_rolls_back_and_cannot_reopen()
    {
        var lifetime = new TestLifetime();
        using var owner = Coordinator(lifetime);
        var pid = await StartSynthetic(owner);
        await using var db = Context(owner);
        await Assert.ThrowsAnyAsync<Exception>(() => Session(owner, db).RunInstallationAsync(async ct =>
        {
            await new UnitOfWork(db).ExecuteInTransactionAsync(async token =>
            {
                await db.Database.ExecuteSqlRawAsync("INSERT INTO first_run_probe VALUES (1)", token);
                await ExecuteAsync($"SELECT pg_terminate_backend({pid})");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO first_run_probe VALUES (2)", token);
            }, ct);
            return true;
        }, default));
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM first_run_probe"));
        Assert.True(lifetime.Stopped.Task.IsCompleted);
        using var replacement = Coordinator();
        await StartSynthetic(replacement);
    }

    [Fact]
    public async Task EF_is_forbidden_to_reopen_a_closed_protected_connection()
    {
        using var owner = Coordinator();
        await StartSynthetic(owner);
        await using var db = Context(owner);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Session(owner, db).RunInstallationAsync(async ct =>
        {
            await db.Database.GetDbConnection().CloseAsync();
            // Deliberately try to bypass the operation's cancellation: interceptor must still reject it.
            await db.Database.ExecuteSqlRawAsync("INSERT INTO first_run_probe VALUES (1)");
            return true;
        }, default));
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM first_run_probe"));
    }

    [Fact]
    public async Task Protected_writes_cannot_use_another_already_open_connection()
    {
        var lifetime = new TestLifetime();
        using var owner = Coordinator(lifetime);
        await StartSynthetic(owner);
        await using var db = Context(owner);
        await using var other = new NpgsqlConnection(ConnectionString);
        await other.OpenAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Session(owner, db).RunInstallationAsync(async ct =>
        {
            db.Database.SetDbConnection(other, contextOwnsConnection: false);
            await db.Database.ExecuteSqlRawAsync("INSERT INTO first_run_probe VALUES (1)", ct);
            return true;
        }, default));
        Assert.True(lifetime.Stopped.Task.IsCompleted);
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM first_run_probe"));
    }

    [Fact]
    public async Task Completion_commits_before_release_and_next_startup_emits_nothing()
    {
        using var owner = Coordinator();
        await StartSynthetic(owner);
        await using (var db = Context(owner))
        {
            await Session(owner, db).RunInstallationAsync(async ct =>
            {
                await new UnitOfWork(db).ExecuteInTransactionAsync(async token =>
                {
                    var state = await new SystemSetupRepository(db).GetAsync(token);
                    state!.MarkInitialized("synthetic-state-transition-only");
                }, ct);
                return true;
            }, default);
        }
        Assert.Equal(true, await ScalarAsync("SELECT is_initialized FROM system_setup_state"));
        Assert.Equal(DBNull.Value, await ScalarAsync("SELECT setup_token_hash FROM system_setup_state"));
        using var next = Coordinator();
        await using var nextDb = Context(next);
        using var output = new StringWriter();
        await WithoutConsole(output, () => new FirstRunSetupService(new SystemSetupRepository(nextDb),
            Configuration(), NullLogger<FirstRunSetupService>.Instance, Session(next, nextDb)).EnsureSetupTokenAsync());
        Assert.True(output.GetStringBuilder().Length == 0);
        Assert.True(await CanAcquireLock());
    }

    [Fact]
    public async Task Simultaneous_requests_wait_and_real_handler_rechecks_initialized_inside_transaction()
    {
        using var owner = Coordinator();
        await StartSynthetic(owner);
        await using var first = Context(owner);
        await using var second = Context(owner);
        var entered = Signal();
        var release = Signal();
        var firstRequest = Session(owner, first).RunInstallationAsync(async ct =>
        {
            await new UnitOfWork(first).ExecuteInTransactionAsync(async token =>
            {
                var state = await new SystemSetupRepository(first).GetAsync(token);
                state!.MarkInitialized("synthetic-transition-only");
                await first.SaveChangesAsync(token);
                entered.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }, ct);
            return true;
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var handler = Handler(owner, second);
        var secondRequest = handler.Handle(Command("synthetic-unused-token"), default);
        try
        {
            Assert.False(secondRequest.IsCompleted);
            using var contender = Coordinator();
            await using var contenderDb = Context(contender);
            // The owner has flushed IsInitialized but not committed: a second process must still fail.
            await Assert.ThrowsAsync<InvalidOperationException>(() => Session(contender, contenderDb)
                .RunStartupAsync(_ => throw new Exception("Must not run before commit"), default));
        }
        finally { release.SetResult(); }
        await firstRequest;
        var result = await secondRequest;
        Assert.False(result.IsSuccess);
        Assert.Equal(ApiResponseCodes.Common.Conflict, result.Code);
    }

    [Fact]
    public async Task Real_handler_rolls_back_intermediate_writes_and_keeps_owner_on_provisioning_failure()
    {
        using var owner = Coordinator();
        await using (var startup = Context(owner))
        {
            await Session(owner, startup).RunStartupAsync(ct => IssueSyntheticState(startup, ct,
                SetupTokenCrypto.Hash("synthetic-test-input")), default);
        }
        await using var db = Context(owner);
        var tenants = new Mock<ITenantRepository>(MockBehavior.Strict);
        tenants.Setup(x => x.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Returns(async (Tenant _, CancellationToken ct) =>
            {
                Assert.NotNull(db.Database.CurrentTransaction);
                await db.Database.ExecuteSqlRawAsync("INSERT INTO first_run_probe VALUES (1)", ct);
            });
        var provisioning = new Mock<ICompanyProvisioningService>(MockBehavior.Strict);
        provisioning.Setup(x => x.EnsureDefaultCompanyAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Synthetic provisioning failure before account persistence"));
        var handler = Handler(owner, db, tenants.Object, provisioning.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Command("synthetic-test-input"), default));
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM first_run_probe"));
        Assert.Equal(false, await ScalarAsync("SELECT is_initialized FROM system_setup_state"));
        Assert.False(await CanAcquireLock());
        // A retry uses the same owner session and observes the unconsumed synthetic state.
        await using var retryDb = Context(owner);
        var invalid = await Handler(owner, retryDb).Handle(Command("synthetic-invalid"), default);
        Assert.Equal(ApiResponseCodes.Common.Unauthorized, invalid.Code);
    }

    [Fact]
    public async Task Commit_failure_rolls_back_state_and_keeps_owner()
    {
        using var owner = Coordinator();
        await StartSynthetic(owner);
        await ExecuteAsync("INSERT INTO first_run_commit_probe VALUES (1)");
        await using var db = Context(owner);
        await Assert.ThrowsAnyAsync<Exception>(() => Session(owner, db).RunInstallationAsync(async ct =>
        {
            await new UnitOfWork(db).ExecuteInTransactionAsync(async token =>
            {
                var state = await new SystemSetupRepository(db).GetAsync(token);
                state!.MarkInitialized("synthetic-transition-only");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO first_run_commit_probe VALUES (1)", token);
            }, ct);
            return true;
        }, default));
        Assert.Equal(false, await ScalarAsync("SELECT is_initialized FROM system_setup_state"));
        Assert.Equal("synthetic-hash-only", await ScalarAsync("SELECT setup_token_hash FROM system_setup_state"));
        Assert.False(await CanAcquireLock());
    }

    [Fact]
    public async Task Failure_after_successful_commit_is_reconciled_without_reconnecting()
    {
        using var owner = Coordinator();
        await StartSynthetic(owner);
        await using var db = Context(owner);
        await Assert.ThrowsAsync<OperationCanceledException>(() => Session(owner, db).RunInstallationAsync<bool>(async ct =>
        {
            await new UnitOfWork(db).ExecuteInTransactionAsync(async token =>
            {
                var state = await new SystemSetupRepository(db).GetAsync(token);
                state!.MarkInitialized("synthetic-transition-only");
            }, ct);
            throw new OperationCanceledException("Synthetic response failure after commit");
        }, default));
        await owner.StartAsync(default);
        var released = false;
        for (var attempt = 0; attempt < 30 && !released; attempt++)
        {
            released = await CanAcquireLock();
            if (!released) await Task.Delay(200);
        }
        Assert.True(released);
        Assert.Equal(true, await ScalarAsync("SELECT is_initialized FROM system_setup_state"));
        await owner.StopAsync(default);
    }

    [Fact]
    public async Task Persistence_failure_prints_nothing_and_does_not_release_owner()
    {
        await ExecuteAsync("ALTER TABLE system_setup_state ADD CONSTRAINT reject_test_write CHECK (false)");
        using var owner = Coordinator();
        await using var db = Context(owner);
        using var output = new StringWriter();
        await WithoutConsole(output, () => Assert.ThrowsAsync<DbUpdateException>(() =>
            new FirstRunSetupService(new SystemSetupRepository(db), Configuration(),
                NullLogger<FirstRunSetupService>.Instance, Session(owner, db)).EnsureSetupTokenAsync()));
        Assert.True(output.GetStringBuilder().Length == 0);
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM system_setup_state"));
        Assert.False(await CanAcquireLock());
    }

    private IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = ConnectionString }).Build();

    private FirstRunOwnershipCoordinator Coordinator(TestLifetime? lifetime = null) =>
        new(Configuration(), lifetime ?? new TestLifetime(), NullLogger<FirstRunOwnershipCoordinator>.Instance);

    private ErpDbContext Context(FirstRunOwnershipCoordinator coordinator) => new(
        new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(ConnectionString)
            .AddInterceptors(new FirstRunConnectionGuard(coordinator), new FirstRunCommandGuard(coordinator)).Options,
        Mock.Of<ICurrentTenant>(), Mock.Of<IPublisher>(), Mock.Of<ICurrentCompany>());

    private static FirstRunSession Session(FirstRunOwnershipCoordinator coordinator, ErpDbContext db) => new(coordinator, db);

    private async Task<int> StartSynthetic(FirstRunOwnershipCoordinator coordinator)
    {
        var pid = 0;
        await using var db = Context(coordinator);
        await Session(coordinator, db).RunStartupAsync(async ct =>
        {
            AssertDedicated(db);
            pid = ((NpgsqlConnection)db.Database.GetDbConnection()).ProcessID;
            await IssueSyntheticState(db, ct);
        }, default);
        return pid;
    }

    private static async Task IssueSyntheticState(ErpDbContext db, CancellationToken ct, string hash = "synthetic-hash-only")
    {
        var repo = new SystemSetupRepository(db);
        var state = await repo.GetAsync(ct);
        if (state is null) { state = SystemSetupState.CreateNew(); await repo.AddAsync(state, ct); }
        state.IssueSetupToken(hash, DateTime.UtcNow.AddMinutes(15));
        await repo.SaveChangesAsync(ct);
    }

    private static void AssertDedicated(ErpDbContext db)
    {
        var settings = new NpgsqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);
        Assert.False(settings.Pooling);
        Assert.False(settings.Multiplexing);
    }

    private CreateInitialAdminHandler Handler(FirstRunOwnershipCoordinator owner, ErpDbContext db,
        ITenantRepository? tenants = null, ICompanyProvisioningService? provisioning = null) =>
        new(new TransactionRepository(db), tenants ?? Mock.Of<ITenantRepository>(),
            Mock.Of<IAccessRepository>(), Mock.Of<IBranchRepository>(), Mock.Of<ICompanyUserBranchRepository>(),
            Mock.Of<IPasswordHasher>(h => h.HashPassword(It.IsAny<string>()) == "synthetic-password-hash"),
            provisioning ?? Mock.Of<ICompanyProvisioningService>(), new UnitOfWork(db), Session(owner, db));

    private static CreateInitialAdminCommand Command(string token) =>
        new("synthetic.owner", "Synthetic", "Test", null, "Synthetic1!", token);

    private sealed class TransactionRepository(ErpDbContext db) : ISystemSetupRepository
    {
        private readonly SystemSetupRepository _inner = new(db);
        public Task<SystemSetupState?> GetAsync(CancellationToken ct = default)
        { Assert.NotNull(db.Database.CurrentTransaction); return _inner.GetAsync(ct); }
        public Task AddAsync(SystemSetupState state, CancellationToken ct = default) => _inner.AddAsync(state, ct);
        public Task SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    private async Task<bool> CanAcquireLock() => Equals(await ScalarAsync(
        "SELECT pg_try_advisory_lock(1514686021, 1)"), true);

    private static async Task WithoutConsole(StringWriter output, Func<Task> operation)
    {
        var original = Console.Out;
        try { Console.SetOut(output); await operation(); }
        finally { Console.SetOut(original); }
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();
        public TaskCompletionSource Stopped { get; } = Signal();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopping.Token;
        public void StopApplication() { _stopping.Cancel(); Stopped.TrySetResult(); }
    }
}
