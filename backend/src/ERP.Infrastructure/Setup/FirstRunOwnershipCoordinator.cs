using System.Collections.Concurrent;
using System.Data;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ERP.Infrastructure.Setup;

/// <summary>
/// One physical PostgreSQL session owns installation until its commit or process shutdown.
/// Never reconnect this session: protected EF operations must fail if it disappears.
/// </summary>
public sealed class FirstRunOwnershipCoordinator : BackgroundService
{
    // Reserved pair in the database's advisory-lock namespace (not a runtime string hash).
    private const int LockNamespace = 1514686021;
    private const int LockResource = 1;
    private enum Phase { Unknown, Owned, Completed, Lost, Stopping }
    private volatile Phase _phase;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<DbContext, byte> _protected = new();
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<FirstRunOwnershipCoordinator> _logger;
    private NpgsqlConnection? _connection;
    private int _backendPid;

    public FirstRunOwnershipCoordinator(IConfiguration configuration, IHostApplicationLifetime lifetime,
        ILogger<FirstRunOwnershipCoordinator> logger)
    {
        _configuration = configuration;
        _lifetime = lifetime;
        _logger = logger;
    }

    internal bool Protects(DbContext? context) => context is not null && _protected.ContainsKey(context);

    internal void AssertProtectedConnection(DbContext? context, System.Data.Common.DbConnection? connection)
    {
        if (Protects(context) && (_phase != Phase.Owned || !ReferenceEquals(connection, _connection)
            || connection?.State != ConnectionState.Open))
        {
            LoseOwnership();
            throw new InvalidOperationException("Protected setup commands must use the live owner session.");
        }
    }

    internal async Task<T> RunAsync<T>(ErpDbContext db, bool startup,
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _shutdown.Token, _lifetime.ApplicationStopping);
        var ct = linked.Token;
        await _gate.WaitAsync(ct);
        string? originalConnectionString = null;
        var bound = false;
        try
        {
            if (_phase == Phase.Unknown)
            {
                if (!startup)
                    throw new InvalidOperationException("First-run ownership has not been established at API startup.");
                await AcquireAsync(ct);
            }
            else if (startup && _phase == Phase.Owned)
                throw new InvalidOperationException("First-run startup has already run in this process.");

            if (_phase == Phase.Completed)
                return await operation(ct); // Normal connections; handler can only return initialized/conflict.
            if (_phase != Phase.Owned)
                throw new InvalidOperationException("First-run ownership is unavailable. Restart the API.");

            await VerifyOwnershipAsync(ct);
            var original = db.Database.GetDbConnection();
            originalConnectionString = original.ConnectionString;
            if (original.State != ConnectionState.Closed || db.Database.CurrentTransaction is not null
                || db.ChangeTracker.Entries().Any())
                throw new InvalidOperationException("First-run requires a fresh, unopened DbContext.");

            db.Database.SetDbConnection(_connection!, contextOwnsConnection: false);
            _protected.TryAdd(db, 0);
            bound = true;
            var result = await operation(ct);
            ct.ThrowIfCancellationRequested();
            await VerifyOwnershipAsync(ct);
            // The operation's UnitOfWork has already committed and disposed its transaction.
            if (db.Database.CurrentTransaction is not null)
                throw new InvalidOperationException("First-run operation left a transaction open.");
            if (await IsInitializedAsync(ct))
                _phase = Phase.Completed;
            return result;
        }
        catch (Exception ex) when (HasConnectionFailure(ex))
        {
            LoseOwnership(); // Includes an unknown commit outcome; never retry/reconnect here.
            throw;
        }
        finally
        {
            try
            {
                if (bound)
                {
                    db.ChangeTracker.Clear();
                    db.Database.SetDbConnection(new NpgsqlConnection(originalConnectionString!), contextOwnsConnection: true);
                    _protected.TryRemove(db, out _);
                }
                if (_phase is Phase.Completed or Phase.Lost or Phase.Stopping)
                    await CloseSessionAsync();
            }
            catch
            {
                LoseOwnership();
                await CloseSessionAsync();
                throw;
            }
            finally
            {
                if (bound) _protected.TryRemove(db, out _);
                _gate.Release();
            }
        }
    }

    private static bool HasConnectionFailure(Exception exception) =>
        exception is NpgsqlException and not PostgresException
        || exception.InnerException is not null && HasConnectionFailure(exception.InnerException);

    private async Task AcquireAsync(CancellationToken ct)
    {
        var settings = new NpgsqlConnectionStringBuilder(
            _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing installation database connection."))
        {
            Pooling = false,
            Multiplexing = false,
            Enlist = false,
            KeepAlive = 5,
            Timeout = 5,
            ApplicationName = "ERP.FirstRun",
        };
        _connection = new NpgsqlConnection(settings.ConnectionString);
        try
        {
            await _connection.OpenAsync(ct);
            if (await IsInitializedAsync(ct))
            {
                _phase = Phase.Completed;
                return;
            }
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(@ns, @resource)", _connection) { CommandTimeout = 5 };
            command.Parameters.AddWithValue("ns", LockNamespace);
            command.Parameters.AddWithValue("resource", LockResource);
            if (!Equals(await command.ExecuteScalarAsync(ct), true))
            {
                if (await IsInitializedAsync(ct))
                {
                    _phase = Phase.Completed;
                    return;
                }
                throw new InvalidOperationException("Another API owns pending first-run installation. This API cannot start.");
            }
            if (await IsInitializedAsync(ct))
            {
                _phase = Phase.Completed;
                return;
            }
            _backendPid = _connection.ProcessID;
            _phase = Phase.Owned;
            _connection.StateChange += (_, args) =>
            {
                if (_phase == Phase.Owned && args.CurrentState != ConnectionState.Open)
                    LoseOwnership();
            };
        }
        catch
        {
            _phase = Phase.Lost;
            await CloseSessionAsync();
            throw;
        }
    }

    private async Task<bool> IsInitializedAsync(CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT is_initialized FROM system_setup_state WHERE id = 1", _connection) { CommandTimeout = 5 };
        return Equals(await command.ExecuteScalarAsync(ct), true);
    }

    private async Task VerifyOwnershipAsync(CancellationToken ct)
    {
        if (_phase != Phase.Owned || _connection?.State != ConnectionState.Open)
        {
            LoseOwnership();
            throw new InvalidOperationException("Installation session was lost. Restart the API.");
        }
        await using var command = new NpgsqlCommand("""
            SELECT pg_backend_pid() = @pid AND EXISTS (
                SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()
                AND classid = @ns::oid AND objid = @resource::oid AND objsubid = 2 AND granted)
            """, _connection) { CommandTimeout = 5 };
        command.Parameters.AddWithValue("pid", _backendPid);
        command.Parameters.AddWithValue("ns", LockNamespace);
        command.Parameters.AddWithValue("resource", LockResource);
        if (!Equals(await command.ExecuteScalarAsync(ct), true))
        {
            LoseOwnership();
            throw new InvalidOperationException("Installation advisory lock was lost. Restart the API.");
        }
    }

    private void LoseOwnership()
    {
        if (_phase is Phase.Completed or Phase.Stopping or Phase.Lost) return;
        _phase = Phase.Lost;
        // Do not log exceptions, connection strings or any setup data.
        _logger.LogCritical("Installation PostgreSQL ownership lost; stopping API.");
        _shutdown.Cancel();
        _lifetime.StopApplication();
    }

    private async Task CloseSessionAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync(); // Non-pooled physical close releases the session lock.
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _gate.WaitAsync(stoppingToken);
                try
                {
                    if (_phase == Phase.Owned)
                    {
                        await VerifyOwnershipAsync(stoppingToken);
                        // Reconcile cancellation/response failure after a successful installation commit.
                        if (await IsInitializedAsync(stoppingToken))
                        {
                            _phase = Phase.Completed;
                            await CloseSessionAsync();
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch { LoseOwnership(); await CloseSessionAsync(); return; }
                finally { _gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdown.Cancel();
        try { await base.StopAsync(cancellationToken); }
        finally
        {
            _phase = Phase.Stopping;
            await CloseSessionAsync();
        }
    }

    public override void Dispose()
    {
        _phase = Phase.Stopping;
        _shutdown.Cancel();
        _connection?.Dispose();
        base.Dispose();
    }
}
